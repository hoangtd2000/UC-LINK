using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UsbComposite.Helpers;
using UsbComposite.Models;
using UsbComposite.Service;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace UsbComposite.Viewmodels
{
    public class CanViewModel : INotifyPropertyChanged
    {
        //private readonly HidCanService _hidService;
        private readonly CanService _canService;

        private readonly Dictionary<string, CancellationTokenSource> _cyclicSendTokens = new Dictionary<string, CancellationTokenSource>();

        public Action ScrollToLatestFrame { get; set; }

        // private Queue<CanFrame> _frameBuffer = new Queue<CanFrame>();
        private Queue<CanFrameEx> _frameBuffer = new Queue<CanFrameEx>();

        private DispatcherTimer _uiUpdateTimer;
        private const int UI_UPDATE_INTERVAL_MS = 50;
        private const int MAX_FRAMES_PER_UPDATE = 100;

        //public ObservableCollection<CanFrame> ReceivedFrames { get; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<CanFrameEx> ReceivedFrames { get; } = new ObservableCollection<CanFrameEx>();



        public ObservableCollection<CanFrame> CanFrames { get; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<byte> DlcOptions { get; } = new ObservableCollection<byte>(Enumerable.Range(0, 9).Select(i => (byte)i));
        public ObservableCollection<CanFrame.CanFrameType> FrameTypeOptions { get; } =
        new ObservableCollection<CanFrame.CanFrameType>
        {
            CanFrame.CanFrameType.Standard,
            CanFrame.CanFrameType.Extended
        };

        public ICommand ConnectCanCommand { get; }
        public ICommand DisconnectCanCommand { get; }
        public ICommand SendCanFrameCommand { get; }
        public ICommand ClearReceiveCommand { get; }

        private CanConfigViewModel _config;
        public CanConfigViewModel Config
        {
            get => _config;
            set { _config = value; OnPropertyChanged(); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDisconnected));
            }
        }

        private bool _isDeviceConnected;
        public bool IsDeviceConnected
        {
            get => _isDeviceConnected;
            set
            {
                _isDeviceConnected = value;
                OnPropertyChanged();
            }
        }


        private void StartCyclicSendWithStopwatch(CanFrame frame)
        {
            string key = $"frame_{frame.FrameIndex}";
            var data = frame.ToBytes();

            // Chuyển string sang int, nếu không hợp lệ thì gán mặc định 1000ms
            if (!int.TryParse(frame.CycleTimeMs, out int intervalMs))
            {
                intervalMs = 1000;
            }

            //Debug.WriteLine($"[StartCyclic] Chuẩn bị gửi: {key} mỗi {intervalMs}ms");

            StopCyclicSend(key);

            var cts = new CancellationTokenSource();
            _cyclicSendTokens[key] = cts;

            Task.Run(() =>
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                long nextTick = sw.ElapsedMilliseconds;

                while (!cts.Token.IsCancellationRequested)
                {
                    long now = sw.ElapsedMilliseconds;

                    if (now >= nextTick)
                    {
                        _canService.SendFrame(data, 0x00);
                        nextTick += intervalMs;
                    }

                    int sleepTime = (int)(nextTick - sw.ElapsedMilliseconds);
                    if (sleepTime > 0)
                        Thread.Sleep(sleepTime);
                }

                sw.Stop();
            }, cts.Token);
        }



        private void StopCyclicSend(string key)
        {
            if (_cyclicSendTokens.TryGetValue(key, out var cts))
            {
                cts.Cancel();
                _cyclicSendTokens.Remove(key);
                //  Debug.WriteLine($"[Cyclic] Stopped cyclic send for Key={key}");
            }
        }

        private void SendCanFrame(CanFrame frame)
        {
            if (!_canService.IsConnected || frame == null)
                return;

            var bytes = frame.ToBytes();
            _canService.SendFrame(bytes, 0x00);
        }



        public bool IsDisconnected => !IsConnected;

        public CanViewModel()
        {
            _canService = new CanService();
            Config = new CanConfigViewModel();

            ConnectCanCommand = new RelayCommand(ConnectCan);
            DisconnectCanCommand = new RelayCommand(DisconnectCan);
            ClearReceiveCommand = new RelayCommand(ExecuteClearReceive);
            SendCanFrameCommand = new RelayCommand<CanFrame>(SendCanFrame);
            CanFrames.CollectionChanged += CanFrames_CollectionChanged;

            _uiUpdateTimer = new DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS);
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

            
            _canService.Disconnected += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (IsConnected)  
                    {
                        IsConnected = false;
                        Debug.WriteLine("Device disconnected due to SendFrame error.");
                    }
                });
            };
            


        }

        private void CanFrames_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CanFrame frame in e.NewItems)
                {
                    frame.PropertyChanged += Frame_PropertyChanged;
                    Debug.WriteLine($"[Bind] Gắn PropertyChanged cho FrameIndex={frame.FrameIndex}");
                }
            }

            if (e.OldItems != null)
            {
                foreach (CanFrame frame in e.OldItems)
                {
                    frame.PropertyChanged -= Frame_PropertyChanged;
                    StopCyclicSend($"frame_{frame.FrameIndex}");
                    Debug.WriteLine($"[Bind] Hủy PropertyChanged cho FrameIndex={frame.FrameIndex}");
                }
            }
        }
     
        private void Frame_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is CanFrame frame)) return;

            string key = $"frame_{frame.FrameIndex}";

            Debug.WriteLine($"[Frame_PropertyChanged] PropertyChanged: {e.PropertyName}, FrameIndex={frame.FrameIndex}, IsCyclic={frame.IsCyclic}, Time={frame.CycleTimeMs}");

            if (e.PropertyName == nameof(CanFrame.IsCyclic))
            {
                if (frame.IsCyclic)
                {
                    if (int.TryParse(frame.CycleTimeMs, out int cycleTime) && cycleTime > 0)
                    {
                        Debug.WriteLine($"[Cycle] Bắt đầu gửi lặp: {key}");
                        StartCyclicSendWithStopwatch(frame);
                    }
                    else
                    {
                        Debug.WriteLine($"[Cycle] CycleTimeMs không hợp lệ hoặc <= 0: {frame.CycleTimeMs}");
                        StopCyclicSend(key);
                    }
                }
                else
                {
                    Debug.WriteLine($"[Cycle] Ngừng gửi: {key}");
                    StopCyclicSend(key);
                }
            }

            if (e.PropertyName == nameof(CanFrame.CycleTimeMs) && frame.IsCyclic)
            {
                Debug.WriteLine($"[Cycle] Cập nhật chu kỳ mới: {frame.CycleTimeMs}ms → Restart");
                StopCyclicSend(key);
                StartCyclicSendWithStopwatch(frame);
            }
        }

        private void ExecuteClearReceive()
        {
            _frameBuffer.Clear();
            ReceivedFrames.Clear();
            Debug.WriteLine("CAN: Đã gọi ClearReceiveCommand");
        }
        private bool _isFrameHandlerAttached = false;


        private void ConnectCan()
        {
            Debug.WriteLine("Đang cố gắng kết nối CAN...");
            bool connected = _canService.Connect();

            if (connected)
            {
                if (!_isFrameHandlerAttached)
                {
                    _canService.FrameReceived += OnFrameReceived;
                    _isFrameHandlerAttached = true;
                    Console.WriteLine("✅ FrameReceived handler đã được gắn.");
                }

                IsConnected = true;
                _uiUpdateTimer.Start();

                SendCanConfigMessage();
            }
            else
            {
                IsConnected = false;
                Debug.WriteLine("❌ Kết nối thiết bị HID thất bại. Kiểm tra thiết bị và driver.");
                MessageBox.Show("Thiết bị đâu ???");
            }
        }

        private void DisconnectCan()
        {
            Debug.WriteLine("Đang cố gắng ngắt kết nối CAN...");

            // Dừng tất cả gửi chu kỳ
            foreach (var token in _cyclicSendTokens.Values)
            {
                token.Cancel();
            }
            _cyclicSendTokens.Clear();

            CanFrames.Clear();

            _uiUpdateTimer.Stop();
            _frameBuffer.Clear();
            ReceivedFrames.Clear();

            if (_canService.IsConnected)
            {
                SendCanDisableMessage();
            }

            if (_isFrameHandlerAttached)
            {
                _canService.FrameReceived -= OnFrameReceived;
                _isFrameHandlerAttached = false;
                Console.WriteLine("✅ FrameReceived handler đã được gỡ.");
            }

            _canService.Disconnect();
            IsConnected = false;
            Debug.WriteLine("CAN đã ngắt kết nối.");
        }

        private void OnFrameReceived(byte[] data)
        {
            if (data == null || data.Length < 18)
                return;

            byte cmd = data[0];
            if (cmd != 0x03)
                return;

            byte rawInfo = data[1];
            byte dlc = (byte)((rawInfo >> 4) & 0x0F);
            bool isExtended = (rawInfo & 0x04) != 0; // Nếu MCU dùng bit khác cho IDE, đổi lại cho đúng

            if (data.Length < 6 + dlc + 4)
                return;

            // Đọc CAN ID 32-bit từ MCU
            uint rawId = ((uint)data[2] << 24) |
                         ((uint)data[3] << 16) |
                         ((uint)data[4] << 8) |
                         data[5];

            // Mask ID theo loại frame
            uint canId;
            string idFormatted;
            if (isExtended)
            {
                canId = rawId & 0x1FFFFFFF;       // Extended 29-bit
                idFormatted = $"0x{canId:X}";     // Hiển thị gọn
            }
            else
            {
                canId = rawId & 0x7FF;            // Standard 11-bit
                idFormatted = $"0x{canId:X}";     // Hiển thị gọn
            }

            var frameType = isExtended ? CanFrame.CanFrameType.Extended : CanFrame.CanFrameType.Standard;

            // Payload
            byte[] payload = new byte[dlc];
            Array.Copy(data, 6, payload, 0, dlc);

            // Timestamp từ MCU
            uint rawCycle = ((uint)data[14] << 24) |
                            ((uint)data[15] << 16) |
                            ((uint)data[16] << 8) |
                            data[17];

            // Log debug
           // Debug.WriteLine($"[CAN RX] ID={idFormatted}, Extended={isExtended}, rawId=0x{rawId:X8}, DLC={dlc}");

            // Tìm frame đã tồn tại (so sánh bằng CanIdAsUInt)
            var existingEx = ReceivedFrames
                .OfType<CanFrameEx>()
                .FirstOrDefault(f => f.CanIdAsUInt == canId && f.FrameType == frameType);

            if (existingEx != null)
            {
                bool isDataChanged =
                    existingEx.Dlc != dlc ||
                    !existingEx.DataBytesHex.Select(b => b.Value)
                        .SequenceEqual(payload.Select(b => b.ToString("X2")));

                if (isDataChanged)
                    existingEx.UpdateData(payload, dlc);

                // 🔹 Tăng count khi nhận frame trùng ID + FrameType
                existingEx.Count += 1;

                // Tính cycle time
                long diff = (long)rawCycle - (long)existingEx.LastTimestampFromMcu;
                if (diff < 0) diff += 0x1_0000_0000; // overflow 32-bit
                existingEx.CycleTimeMsInt = diff / 10.0;
                existingEx.LastTimestampFromMcu = rawCycle;

                // Cập nhật UI
                existingEx.Timestamp = DateTime.Now;
                existingEx.OnPropertyChanged(nameof(existingEx.Timestamp));
                existingEx.OnPropertyChanged(nameof(existingEx.CycleTimeMsDisplay));
            }
            else
            {
                // ⚠️ GÁN THỨ TỰ: FrameType TRƯỚC, CanId SAU
                var newFrame = new CanFrameEx
                {
                    Timestamp = DateTime.Now,
                    FrameType = frameType,
                    LastTimestampFromMcu = rawCycle,
                    CycleTimeMsInt = 1000,      // Mặc định
                    Count = 1
                };

                newFrame.CanId = idFormatted;     // Gán sau khi đã có FrameType
                newFrame.UpdateData(payload, dlc);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReceivedFrames.Add(newFrame);
                });
            }
        }

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            int processed = 0;

            while (_frameBuffer.Count > 0 && processed < MAX_FRAMES_PER_UPDATE)
            {
                var frame = _frameBuffer.Dequeue();
                processed++;

                // 🔹 So sánh bằng CanIdAsUInt + FrameType
                var existing = ReceivedFrames.FirstOrDefault(f =>
                    f.CanIdAsUInt == frame.CanIdAsUInt &&
                    f.FrameType == frame.FrameType);

                if (existing is CanFrameEx existingEx && frame is CanFrameEx newEx)
                {
                    // ⚠️ GÁN THỨ TỰ: FrameType TRƯỚC, CanId SAU
                    existingEx.FrameType = newEx.FrameType;
                    existingEx.CanId = newEx.CanId;

                    bool isDataDifferent = existingEx.Dlc != newEx.Dlc ||
                                           !existingEx.DataBytesHex.Select(b => b.Value)
                                               .SequenceEqual(newEx.DataBytesHex.Select(b => b.Value));

                    if (isDataDifferent)
                    {
                        existingEx.UpdateData(
                            newEx.DataBytesHex.Select(b => Convert.ToByte(b.Value, 16)).ToArray(),
                            newEx.Dlc
                        );
                    }

                    existingEx.CycleTimeMsInt = newEx.CycleTimeMsInt;
                    existingEx.Timestamp = newEx.Timestamp;

                    existingEx.OnPropertyChanged(nameof(existingEx.CanId));
                    existingEx.OnPropertyChanged(nameof(existingEx.CycleTimeMsDisplay));
                    existingEx.OnPropertyChanged(nameof(existingEx.Timestamp));
                    existingEx.Count += 1;
                }
                else
                {
                    ReceivedFrames.Add(frame);
                    ScrollToLatestFrame?.Invoke();
                }
            }
        }







        private const byte HID_OUTPUT_REPORT_ID = 0x00;

        private void SendCanConfigMessage()
        {
            if (!_canService.IsConnected)
            {
             //   Debug.WriteLine("Không thể gửi cấu hình: Dịch vụ HID chưa kết nối.");
                MessageBox.Show("Thiết bị đâu ???");
                return;
            }

            ushort baudRate = (ushort)Config.SelectedBaudRate;
            byte filterType = Config.IsStandardIdFilter ? (byte)0x00 : (byte)0x04;
            ushort samplePointValue = (ushort)(Config.SamplePoint * 10.0f);

            uint filterFromId = 0;
            if (!string.IsNullOrEmpty(Config.FilterFromId))
            {
                try
                {
                    filterFromId = Convert.ToUInt32(Config.FilterFromId.Replace("0x", ""), 16);

                    // Kiểm tra giới hạn ID
                    if (Config.IsStandardIdFilter)
                    {
                        if (filterFromId > 0x7FF)
                            throw new ArgumentOutOfRangeException(nameof(filterFromId), $"FilterFromId không được lớn hơn 0x7FF (2047).");
                    }
                    else
                    {
                        if (filterFromId > 0x1FFFFFFF)
                            throw new ArgumentOutOfRangeException(nameof(filterFromId), $"FilterFromId không được lớn hơn 0x1FFFFFFF (536870911).");
                    }
                }
                catch (FormatException)
                {
                   
                    Config.FilterFromId = Config.IsStandardIdFilter ? "0" : "0";
                    filterFromId = 0;
                }
                catch (ArgumentOutOfRangeException)
                {
                   
                    Config.FilterFromId = Config.IsStandardIdFilter ? "0" : "0";
                    filterFromId = 0;
                }
            }

            uint filterToId = 0;
            if (!string.IsNullOrEmpty(Config.FilterToId))
            {
                try
                {
                    filterToId = Convert.ToUInt32(Config.FilterToId.Replace("0x", ""), 16);

                    if (Config.IsStandardIdFilter)
                    {
                        if (filterToId > 0x7FF)
                            throw new ArgumentOutOfRangeException(nameof(filterToId), $"FilterToId không được lớn hơn 0x7FF (2047).");
                    }
                    else
                    {
                        if (filterToId > 0x1FFFFFFF)
                            throw new ArgumentOutOfRangeException(nameof(filterToId), $"FilterToId không được lớn hơn 0x1FFFFFFF (536870911).");
                    }
                }
                catch (FormatException)
                {
                    Config.FilterToId = Config.IsStandardIdFilter ? "7FF" : "1FFFFFFF";
                    filterToId = Config.IsStandardIdFilter ? 0x7FFu : 0x1FFFFFFFu;
                }
                catch (ArgumentOutOfRangeException)
                {
                    Config.FilterToId = Config.IsStandardIdFilter ? "7FF" : "1FFFFFFF";
                    filterToId = Config.IsStandardIdFilter ? 0x7FFu : 0x1FFFFFFFu;
                }
            }


            uint range = filterToId - filterFromId + 1;
            bool isPowerOfTwo = (range & (range - 1)) == 0;

            // Nếu không phải lũy thừa của 2, làm tròn lên
            if (!isPowerOfTwo)
            {
                uint nextPowerOfTwo = 1;
                while (nextPowerOfTwo < range) nextPowerOfTwo <<= 1;
                range = nextPowerOfTwo;
            }

            uint mask = ~((uint)(range - 1));
            uint alignedFromId = filterFromId & mask;
            uint alignedToId = alignedFromId + range - 1;

            bool adjusted = (filterFromId != alignedFromId || filterToId != alignedToId);

            // Nếu cần điều chỉnh
            if (adjusted)
            {
                filterFromId = alignedFromId;
                filterToId = alignedToId;

                Config.FilterFromId = "0x" + filterFromId.ToString("X");
                Config.FilterToId = "0x" + filterToId.ToString("X");

            }



            byte[] configMessage = new byte[_canService.GetHidReportPayloadSize()];
            Array.Clear(configMessage, 0, configMessage.Length);
            // Header
            configMessage[0] = 0x01;

            configMessage[1] = (byte)(baudRate & 0xFF);
            configMessage[2] = (byte)((baudRate >> 8) & 0xFF);

            //  samplePoint 
            configMessage[3] = (byte)(samplePointValue & 0xFF);
            configMessage[4] = (byte)((samplePointValue >> 8) & 0xFF);

            //  filterType 
            configMessage[5] = filterType;

            // ➕  filterFromId 
            configMessage[6] = (byte)(filterFromId & 0xFF);
            configMessage[7] = (byte)((filterFromId >> 8) & 0xFF);
            configMessage[8] = (byte)((filterFromId >> 16) & 0xFF);
            configMessage[9] = (byte)((filterFromId >> 24) & 0xFF);

            // ➕  filterToId 
            configMessage[10] = (byte)(filterToId & 0xFF);
            configMessage[11] = (byte)((filterToId >> 8) & 0xFF);
            configMessage[12] = (byte)((filterToId >> 16) & 0xFF);
            configMessage[13] = (byte)((filterToId >> 24) & 0xFF);

            _canService.SendFrame(configMessage, HID_OUTPUT_REPORT_ID);
            Debug.WriteLine("Sent CAN Config message (Payload): " + BitConverter.ToString(configMessage));
        }

        private void SendCanDisableMessage()
        {
            if (!_canService.IsConnected)
            {
                Debug.WriteLine("Không thể gửi lệnh tắt: Dịch vụ HID chưa kết nối.");
                return;
            }

            byte[] disableMessage = new byte[_canService.GetHidReportPayloadSize()];
            Array.Clear(disableMessage, 0, disableMessage.Length);
            disableMessage[0] = 0x01;

            _canService.SendFrame(disableMessage, HID_OUTPUT_REPORT_ID);
            Debug.WriteLine("Sent CAN Disable message (Payload): " + BitConverter.ToString(disableMessage));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
