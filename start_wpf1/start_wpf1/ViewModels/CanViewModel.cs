using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using start_wpf1.Helpers;
using start_wpf1.Models;
using start_wpf1.Service;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace start_wpf1.ViewModels
{
    public class CanViewModel : INotifyPropertyChanged
    {
        private readonly HidCanService _hidService;

        private readonly Dictionary<string, CancellationTokenSource> _cyclicSendTokens = new Dictionary<string, CancellationTokenSource>();

        public Action ScrollToLatestFrame { get; set; }

        private Queue<CanFrame> _frameBuffer = new Queue<CanFrame>();
        private DispatcherTimer _uiUpdateTimer;
        private const int UI_UPDATE_INTERVAL_MS = 50;
        private const int MAX_FRAMES_PER_UPDATE = 100;

        public ObservableCollection<CanFrame> ReceivedFrames { get; } = new ObservableCollection<CanFrame>();
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
        private void StartCyclicSendWithStopwatch(CanFrame frame)
        {
            string key = $"frame_{frame.FrameIndex}";
            var data = frame.ToBytes();
            int intervalMs = frame.CycleTimeMs;

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
                        _hidService.SendFrame(data, 0x00);
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
            if (!_hidService.IsConnected || frame == null)
                return;

            string key = $"frame_{frame.FrameIndex}";

            if (frame.IsCyclic && frame.CycleTimeMs > 0)
            {
                if (_cyclicSendTokens.ContainsKey(key))
                {
                    StopCyclicSend(key);
                }
                else
                {
                    StartCyclicSendWithStopwatch(frame);
                }
            }
            else
            {
                if (_cyclicSendTokens.ContainsKey(key))
                {
                    StopCyclicSend(key);
                }

                var bytes = frame.ToBytes();
                _hidService.SendFrame(bytes, 0x00);
              //  Debug.WriteLine($"[OneShot] Sent frame Key={key} at {DateTime.Now:HH:mm:ss.fff}: {BitConverter.ToString(bytes)}");
            }
        }


        public bool IsDisconnected => !IsConnected;

        public CanViewModel()
        {
            _hidService = new HidCanService();
            Config = new CanConfigViewModel();

            ConnectCanCommand = new RelayCommand(ConnectCan);
            DisconnectCanCommand = new RelayCommand(DisconnectCan);
            SendCanFrameCommand = new RelayCommand<CanFrame>(SendCanFrame);

            _uiUpdateTimer = new DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS);
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
        }

        private bool _isFrameHandlerAttached = false;

        private void ConnectCan()
        {
            Debug.WriteLine("Đang cố gắng kết nối CAN...");
            bool connected = _hidService.Connect();

            if (connected)
            {
                if (!_isFrameHandlerAttached)
                {
                    _hidService.FrameReceived += OnFrameReceived;
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

            if (_hidService.IsConnected)
            {
                SendCanDisableMessage();
            }

            if (_isFrameHandlerAttached)
            {
                _hidService.FrameReceived -= OnFrameReceived;
                _isFrameHandlerAttached = false;
                Console.WriteLine("✅ FrameReceived handler đã được gỡ.");
            }

            _hidService.Disconnect();
            IsConnected = false;
            Debug.WriteLine("CAN đã ngắt kết nối.");
        }
        private void OnFrameReceived(byte[] data)
        {
         //   Console.WriteLine($"🟢 Frame Received Handler called at {DateTime.Now:HH:mm:ss.fff}");

            if (data == null || data.Length < 14)
                return;

            byte cmd = data[0];
         //   Console.WriteLine($"🔍 FrameReceived invoked, CMD: {cmd:X2}");

            if (cmd != 0x03)
                return;

            // Byte 1: DLC (4 bit cao), FrameType (1 bit thấp)
            byte rawInfo = data[1];
            byte dlc = (byte)((rawInfo >> 4) & 0x0F);
            bool isExtended = (rawInfo & 0x08) != 0;

            if (data.Length < 6 + dlc + 4)
            {
             //   Debug.WriteLine("❌ Not enough data for full frame.");
                return;
            }

            // Byte 2~5: CAN ID (4 bytes Big-Endian)
            uint canId = ((uint)data[2] << 24) |
                         ((uint)data[3] << 16) |
                         ((uint)data[4] << 8) |
                         data[5];

            // Byte 6~(6+dlc-1): Data Payload
            byte[] payload = new byte[dlc];
            Array.Copy(data, 6, payload, 0, dlc);

            // Byte 6+dlc ~ 6+dlc+3: CycleTime (4 bytes Big-Endian)
          //  int cycleOffset = 6 + dlc;
            uint rawCycle = ((uint)data[14] << 24) |
                            ((uint)data[15] << 16) |
                            ((uint)data[16] << 8) |
                            data[17];
            int cycleTimeMs = (int)(rawCycle * 0.1); // mỗi đơn vị = 100us → 0.1ms

            string idFormatted = isExtended
                ? $"0x{canId:X8}"
                : $"0x{(canId & 0x7FF):X3}";

            var newFrame = new CanFrame
            {
                Timestamp = DateTime.Now,
                CanId = idFormatted,
                FrameType = isExtended ? CanFrame.CanFrameType.Extended : CanFrame.CanFrameType.Standard,
                Dlc = dlc,
                CycleTimeMs = cycleTimeMs,
                IsCyclic = rawCycle > 0, // flag cho cột "Cycle"
                DataBytesHex = new ObservableCollection<BindableByte>(
                    payload.Select(b => new BindableByte { Value = b.ToString("X2") })
                )
            };

            _frameBuffer.Enqueue(newFrame);
        }


        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            int framesAddedThisTick = 0;

            try
            {
                while (_frameBuffer.Count > 0 && framesAddedThisTick < MAX_FRAMES_PER_UPDATE)
                {
                    var frame = _frameBuffer.Dequeue();
                    ReceivedFrames.Add(frame);
                    framesAddedThisTick++;
                }

                if (framesAddedThisTick > 0)
                {
                    ScrollToLatestFrame?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UiUpdateTimer_Tick: {ex.Message}");
            }
        }

        private const byte HID_OUTPUT_REPORT_ID = 0x00;

        private void SendCanConfigMessage()
        {
            if (!_hidService.IsConnected)
            {
                Debug.WriteLine("Không thể gửi cấu hình: Dịch vụ HID chưa kết nối.");
                return;
            }

            ushort baudRate = (ushort)Config.SelectedBaudRate;
            byte filterType = Config.IsStandardIdFilter ? (byte)0x00 : (byte)0x01;

            uint filterFromId = 0;
            if (!string.IsNullOrEmpty(Config.FilterFromId))
            {
                try
                {
                    filterFromId = Convert.ToUInt32(Config.FilterFromId.Replace("0x", ""), 16);
                }
                catch (FormatException)
                {
                    Debug.WriteLine($"Định dạng FilterFromId không hợp lệ: {Config.FilterFromId}");
                }
            }

            uint filterToId = 0;
            if (!string.IsNullOrEmpty(Config.FilterToId))
            {
                try
                {
                    filterToId = Convert.ToUInt32(Config.FilterToId.Replace("0x", ""), 16);
                }
                catch (FormatException)
                {
                    Debug.WriteLine($"Định dạng FilterToId không hợp lệ: {Config.FilterToId}");
                }
            }

            byte[] configMessage = new byte[_hidService.GetHidReportPayloadSize()];
            Array.Clear(configMessage, 0, configMessage.Length);

            configMessage[0] = 0x01;

            configMessage[1] = (byte)(baudRate & 0xFF);
            configMessage[2] = (byte)((baudRate >> 8) & 0xFF);
            configMessage[3] = filterType;

            configMessage[4] = (byte)(filterFromId & 0xFF);
            configMessage[5] = (byte)((filterFromId >> 8) & 0xFF);
            configMessage[6] = (byte)((filterFromId >> 16) & 0xFF);
            configMessage[7] = (byte)((filterFromId >> 24) & 0xFF);

            configMessage[8] = (byte)(filterToId & 0xFF);
            configMessage[9] = (byte)((filterToId >> 8) & 0xFF);
            configMessage[10] = (byte)((filterToId >> 16) & 0xFF);
            configMessage[11] = (byte)((filterToId >> 24) & 0xFF);

            _hidService.SendFrame(configMessage, HID_OUTPUT_REPORT_ID);
            Debug.WriteLine("Sent CAN Config message (Payload): " + BitConverter.ToString(configMessage));
        }

        private void SendCanDisableMessage()
        {
            if (!_hidService.IsConnected)
            {
                Debug.WriteLine("Không thể gửi lệnh tắt: Dịch vụ HID chưa kết nối.");
                return;
            }

            byte[] disableMessage = new byte[_hidService.GetHidReportPayloadSize()];
            Array.Clear(disableMessage, 0, disableMessage.Length);
            disableMessage[0] = 0x01;

            _hidService.SendFrame(disableMessage, HID_OUTPUT_REPORT_ID);
            Debug.WriteLine("Sent CAN Disable message (Payload): " + BitConverter.ToString(disableMessage));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
