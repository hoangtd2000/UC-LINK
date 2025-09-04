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



        private DispatcherTimer _errorDecayTimer;
        private DateTime _lastErrorUtc = DateTime.MinValue;
        private const int ERROR_DECAY_MS = 50;


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

        private string _lastErrorMessage = "";
        public string LastErrorMessage
        {
            get { return _lastErrorMessage; }
            set { if (_lastErrorMessage == value) return; _lastErrorMessage = value; OnPropertyChanged(); }
        }
        public System.Windows.Visibility ErrorVisibility
        {
            get { return string.IsNullOrEmpty(LastErrorMessage) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible; }
        }



        private CanConfigViewModel _config;
        public CanConfigViewModel Config
        {
            get { return _config; }
            set
            {
                if (ReferenceEquals(_config, value)) return;

                if (_config != null)
                    _config.PropertyChanged -= OnConfigPropertyChanged;

                _config = value;
                OnPropertyChanged();

                if (_config != null)
                {
                    _config.PropertyChanged += OnConfigPropertyChanged;
                    UpdateBaudDisplay();
                }
                else
                {
                    CurrentBaudRate = "—";
                }
            }
        }

        private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedBaudRate")
                UpdateBaudDisplay();
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged("IsDisconnected");
                OnPropertyChanged("BaudrateVisibility"); // nếu có

                UpdateConnectionDisplay();

                if (!_isConnected) // ngắt kết nối → dừng poll
                {
                    
                }
            }
        }
        public System.Windows.Visibility BaudrateVisibility
        {
            get { return IsConnected ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
        }
        public bool IsDisconnected
        {
            get { return !IsConnected; }
        }

        private bool _isDeviceConnected;
        public bool IsDeviceConnected
        {
            get { return _isDeviceConnected; }
            set
            {
                if (_isDeviceConnected == value) return;
                _isDeviceConnected = value;
                OnPropertyChanged();
            }
        }

        // ====== 2 string hiển thị + 1 Brush cho màu ======
        private string _currentBaudRate = "—";
        public string CurrentBaudRate
        {
            get { return _currentBaudRate; }
            set
            {
                if (_currentBaudRate == value) return;
                _currentBaudRate = value;
                OnPropertyChanged();
            }
        }

        private string _connectionStatusText = "Chưa kết nối";
        public string ConnectionStatusText
        {
            get { return _connectionStatusText; }
            set
            {
                if (_connectionStatusText == value) return;
                _connectionStatusText = value;
                OnPropertyChanged();
            }
        }

        private Brush _connectionStatusBrush = Brushes.IndianRed;
        public Brush ConnectionStatusBrush
        {
            get { return _connectionStatusBrush; }
            set
            {
                if (_connectionStatusBrush == value) return;
                _connectionStatusBrush = value;
                OnPropertyChanged();
            }
        }

        // ====== Helpers ======
        private void UpdateBaudDisplay()
        {
            if (Config != null)
                CurrentBaudRate = Config.SelectedBaudRate.ToString() + " kbit/s";
            else
                CurrentBaudRate = "—";
        }

        private void UpdateConnectionDisplay()
        {
            ConnectionStatusText = IsConnected ? "Connected to hardware UC-LINK " : "Not connect";
            ConnectionStatusBrush = IsConnected ? Brushes.Green : Brushes.Red;
        }


        private string _statusText = "OK";
        public string StatusText
        {
            get { return _statusText; }
            set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); }
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
            if (!_canService.IsConnected || frame == null || string.IsNullOrWhiteSpace(frame.CanId))
                return;

            var bytes = frame.ToBytes();
            _canService.SendFrame(bytes, 0x00);
        }



        //public bool IsDisconnected => !IsConnected;

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


            _errorDecayTimer = new DispatcherTimer();
            _errorDecayTimer.Interval = TimeSpan.FromMilliseconds(ERROR_DECAY_MS);
            _errorDecayTimer.Tick += ErrorDecayTimer_Tick;


            UpdateBaudDisplay();
            UpdateConnectionDisplay();
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


        private void ErrorDecayTimer_Tick(object sender, EventArgs e)
        {
            if (_lastErrorUtc == DateTime.MinValue)
            {
                _errorDecayTimer.Stop();
                return;
            }

            double elapsed = (DateTime.UtcNow - _lastErrorUtc).TotalMilliseconds;
            if (elapsed >= ERROR_DECAY_MS)
            {
                // Sau 10ms không có lỗi mới -> hiển thị OK
                StatusText = "OK";

                _lastErrorUtc = DateTime.MinValue;
                _errorDecayTimer.Stop();
            }
        }

        // Gọi khi nhận lỗi non-critical (0x01 / 0x02 không có BOF)
        // Timer chỉ chạy khi có lỗi để không tốn CPU lúc bình thường
        private void ShowTransientError(string message)
        {
            StatusText = message;
            _lastErrorUtc = DateTime.UtcNow;
            if (!_errorDecayTimer.IsEnabled) _errorDecayTimer.Start();
        }

        // Gọi khi nhận OK rõ ràng từ thiết bị (err == 0) hoặc khi muốn reset ngay
        private void ShowOkNow()
        {
            StatusText = "OK";
            _lastErrorUtc = DateTime.MinValue;
            if (_errorDecayTimer.IsEnabled) _errorDecayTimer.Stop();
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

            // KHÔNG xóa các frame
            // CanFrames.Clear();
            // _frameBuffer.Clear();
            // ReceivedFrames.Clear();

            _uiUpdateTimer.Stop();

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


      



        // Hiển thị lỗi lên UI và log
        private void SetErrorUI(string message, bool isCritical)
        {
            LastErrorMessage = message;
            ConnectionStatusText = message;
            ConnectionStatusBrush = Brushes.IndianRed;

            OnPropertyChanged("LastErrorMessage");
            OnPropertyChanged("ConnectionStatusText");
            OnPropertyChanged("ConnectionStatusBrush");
            OnPropertyChanged("ErrorVisibility"); // nếu bạn có dùng ErrorVisibility

            System.Diagnostics.Debug.WriteLine("[HID/CAN ERROR] " + message);

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, "CAN/HID Error", MessageBoxButton.OK,
                        isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning);
                });
            }
            catch { /* ignore if no UI */ }
        }
        /*
        private void HandleConfigError01(byte[] data)
        {
            if (data == null || data.Length <= 12)
            {
                ShowTransientError("[CFG] Dữ liệu lỗi không hợp lệ (thiếu data[12])");
                return;
            }

            byte code = data[12];

            // Map theo firmware của bạn; ví dụ:
            string msg;
            switch (code)
            {
                case 0x00: msg = "[CFG] OK"; break;
                case 0x01: msg = "[CFG] Baudrate không hợp lệ"; break;
                case 0x02: msg = "[CFG] Sample point không hợp lệ"; break;
                case 0x03: msg = "[CFG] Khoảng Filter ID không hợp lệ"; break;
                case 0x04: msg = "[CFG] Thiết bị bận (BUSY)"; break;
                case 0x05: msg = "[CFG] Buffer đầy"; break;
                case 0x06: msg = "[CFG] USB HID truyền thất bại"; break;
                case 0x07: msg = "[CFG] CAN controller lỗi khởi tạo"; break;
                default: msg = "[CFG] Unknown error 0x" + code.ToString("X2"); break;
            }

            if (code == 0x00)
                ShowOkNow();             // Nếu firmware báo OK rõ ràng -> trả OK ngay
            else
                ShowTransientError(msg); // Lỗi -> hiển thị rồi tự về OK sau 10ms nếu yên ắng
        }

        */
        // Decode các bit lỗi của cmd 0x01 (data[12])
        private string DecodeConfigErrorFlags(byte flags)
        {
            if (flags == 0x00)
                return "[CFG] OK";

            var parts = new System.Collections.Generic.List<string>();

            // bit mapping theo struct bạn đưa
            if ((flags & (1 << 0)) != 0) parts.Add("TimerRxCanStart");
            if ((flags & (1 << 1)) != 0) parts.Add("TimestempStart");   // bạn viết 'Timestemp', giữ nguyên :)
            if ((flags & (1 << 2)) != 0) parts.Add("ConfigBaudrate");
            if ((flags & (1 << 3)) != 0) parts.Add("ConfigFilter");
            if ((flags & (1 << 4)) != 0) parts.Add("CanStart");
            if ((flags & (1 << 5)) != 0) parts.Add("TimerRxCanStop");
            if ((flags & (1 << 6)) != 0) parts.Add("TimestempStop");
            if ((flags & (1 << 7)) != 0) parts.Add("CanStop");

            // Ghép thông điệp
            return "[CFG] Error: " + string.Join(", ", parts.ToArray()) + " (0x" + flags.ToString("X2") + ")";
        }

        private void HandleConfigError01(byte[] data)
        {
            if (data == null || data.Length <= 12)
            {
                ShowTransientError("[CFG] Dữ liệu lỗi không hợp lệ (thiếu data[12])");
                return;
            }

            byte flags = data[12];
            string msg = DecodeConfigErrorFlags(flags);

            if (flags == 0x00)
                ShowOkNow();          // không có lỗi → OK ngay
            else
                ShowTransientError(msg); // có lỗi → hiển thị rồi tự về OK sau timeout 1-shot
        }


        private void HandleCanSendError02(byte[] data)
        {
            if (data == null || data.Length <= 17)
            {
                ShowTransientError("[RUN] Dữ liệu lỗi không hợp lệ (thiếu data[14..17])");
                return;
            }

            uint err = ((uint)data[14] << 24) |
                       ((uint)data[15] << 16) |
                       ((uint)data[16] << 8) |
                       (uint)data[17];

            if (err == 0)
            {
                ShowOkNow(); // thiết bị báo OK -> hiển thị OK ngay
                return;
            }

            bool ewg = (err & 0x00000001u) != 0;
            bool epv = (err & 0x00000002u) != 0;
            bool bof = (err & 0x00000004u) != 0;

            string msg = "[RUN] " +
                         (ewg ? "EWG " : "") +
                         (epv ? "EPV " : "") +
                         (bof ? "BOF " : "");
            msg = msg.Trim();

            if (bof)
            {
                // BỎ TICK gửi chu kỳ rồi ngắt kết nối — không cần poll 10ms
                try
                {
                    foreach (var f in CanFrames) f.IsCyclic = false; // bỏ tích IsCyclic
                }
                catch { /* ignore */ }

                StatusText = msg;       // có thể giữ thông điệp lỗi tức thời
                _lastErrorUtc = DateTime.MinValue;
                if (_errorDecayTimer.IsEnabled) _errorDecayTimer.Stop();

                DisconnectCan();
                return;
            }

            // Non-critical: hiển thị lỗi rồi tự về OK nếu 10ms không lỗi mới
            ShowTransientError(msg);
        }



        private void OnFrameReceived(byte[] data)
        {
            if (data == null || data.Length < 18)
                return;

            byte cmd = data[0];




            /// Ưu tiên xử lý các header lỗi
            if (cmd == 0x01)
            {
                HandleConfigError01(data);
                return;
            }
            if (cmd == 0x02)
            {
                HandleCanSendError02(data);
                return;
            }

            // 🔵 Header khung CAN
            if (cmd != 0x03) return;





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
