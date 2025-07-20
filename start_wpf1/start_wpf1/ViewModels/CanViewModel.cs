

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using start_wpf1.Helpers;
using start_wpf1.Models;
using start_wpf1.Service;
using System.Windows.Threading; // Thêm using này
using System.Collections.Generic;
using System.Linq;


namespace start_wpf1.ViewModels
{
    public class CanViewModel : INotifyPropertyChanged
    {
        private readonly HidCanService _hidService;
        private Dictionary<CanFrame, DispatcherTimer> _cyclicSenders = new Dictionary<CanFrame, DispatcherTimer>();
        public Action ScrollToLatestFrame { get; set; }

        // Thêm buffer và timer để xử lý hiển thị mượt mà hơn
        private Queue<CanFrame> _frameBuffer = new Queue<CanFrame>();
        private DispatcherTimer _uiUpdateTimer;
        private const int UI_UPDATE_INTERVAL_MS = 50; // Cập nhật UI mỗi 50ms (có thể điều chỉnh)
        private const int MAX_FRAMES_PER_UPDATE = 100; // Giới hạn số lượng frame thêm vào mỗi lần cập nhật UI


        public ObservableCollection<CanFrame> ReceivedFrames { get; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<CanFrame> CanFrames { get; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<byte> DlcOptions { get; } = new ObservableCollection<byte>(Enumerable.Range(0, 9).Select(i => (byte)i)); // DLC có thể từ 0-8 cho CAN STD hoặc EXT


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
        private void SendCanFrame(CanFrame frame)
        {
            if (!_hidService.IsConnected || frame == null)
                return;

            if (frame.IsCyclic)
            {
                // Toggle: Nếu đang gửi rồi → dừng lại
                if (_cyclicSenders.ContainsKey(frame))
                {
                    _cyclicSenders[frame].Stop();
                    _cyclicSenders.Remove(frame);
                    return;
                }

                // Tạo timer gửi liên tục
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(frame.CycleTimeMs)
                };

                timer.Tick += (s, e) =>
                {
                    var bytes = frame.ToBytes();
                    _hidService.SendFrame(bytes);
                    System.Diagnostics.Debug.WriteLine("Sent cyclic frame: " + BitConverter.ToString(bytes));
                };

                _cyclicSenders[frame] = timer;
                timer.Start();
            }
            else
            {
                // Gửi 1 lần
                var bytes = frame.ToBytes();
                _hidService.SendFrame(bytes);
                System.Diagnostics.Debug.WriteLine("Sent one-shot frame: " + BitConverter.ToString(bytes));
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

            // Khởi tạo DispatcherTimer cho việc cập nhật UI
            _uiUpdateTimer = new DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS);
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
        }

       


        /*
    private void ConnectCan()
        {
            // Gửi bản tin cấu hình CAN (header 0x01) trước khi kết nối HID thực sự
            SendCanConfigMessage();

           
            bool connected = _hidService.Connect();

            if (connected)
            {
                if (!_isFrameHandlerAttached)
                {
                    _hidService.FrameReceived += OnFrameReceived;
                    _isFrameHandlerAttached = true;
                    Console.WriteLine("✅ FrameReceived handler gán lần đầu");
                }

                IsConnected = true;
                _uiUpdateTimer.Start(); // Bắt đầu timer cập nhật UI khi kết nối thành công
            }
            else
            {
                IsConnected = false;
            }
        }

        private byte[] BuildShutdownCanFrame()
        {
            // Có thể chỉ cần gửi header 0x01 và một byte đặc biệt để báo shutdown
            return new byte[] { 0x01, 0xFF }; // Ví dụ: 0xFF = shutdown command
        }
        private void DisconnectCan()
        {
            if (_isFrameHandlerAttached)
            {
                _hidService.FrameReceived -= OnFrameReceived;
                _isFrameHandlerAttached = false;
            }

            _uiUpdateTimer.Stop(); // Dừng timer cập nhật UI khi ngắt kết nối
            _frameBuffer.Clear(); // Xóa các frame đang chờ trong buffer
            ReceivedFrames.Clear(); // Xóa các frame đang hiển thị trên UI để làm sạch


            _hidService.Disconnect();
            IsConnected = false;
            // Gửi bản tin hủy CAN (header 0x01 với dữ liệu hủy)
            SendCanDisableMessage();
            
        }
        */
        private bool _isFrameHandlerAttached = false;

        private void ConnectCan()
        {
            System.Diagnostics.Debug.WriteLine("Đang cố gắng kết nối CAN...");
            // BƯỚC 1: Kết nối thiết bị HID trước
            bool connected = _hidService.Connect();

            if (connected)
            {
                // BƯỚC 2: Chỉ gắn handler nhận frame nếu kết nối thành công
                if (!_isFrameHandlerAttached)
                {
                    _hidService.FrameReceived += OnFrameReceived;
                    _isFrameHandlerAttached = true;
                    Console.WriteLine("✅ FrameReceived handler đã được gắn.");
                }

                IsConnected = true;
                _uiUpdateTimer.Start(); // Bắt đầu timer cập nhật UI khi kết nối thành công

                // BƯỚC 3: Gửi bản tin cấu hình CAN SAU KHI kết nối HID đã được thiết lập và sẵn sàng
                SendCanConfigMessage();
            }
            else
            {
                IsConnected = false;
                System.Diagnostics.Debug.WriteLine("❌ Kết nối thiết bị HID thất bại. Kiểm tra thiết bị và driver.");
            }
        }

        private void DisconnectCan()
        {
            System.Diagnostics.Debug.WriteLine("Đang cố gắng ngắt kết nối CAN...");

            // Dừng tất cả các bộ gửi cyclic đang hoạt động
            foreach (var timer in _cyclicSenders.Values)
            {
                timer.Stop();
            }
            _cyclicSenders.Clear();
            CanFrames.Clear(); // Xóa các frame đang chờ gửi

            _uiUpdateTimer.Stop(); // Dừng timer cập nhật UI khi ngắt kết nối
            _frameBuffer.Clear(); // Xóa các frame đang chờ trong buffer
            ReceivedFrames.Clear(); // Xóa các frame đang hiển thị trên UI để làm sạch


            // BƯỚC 1: Gửi bản tin hủy CAN (header 0x01 với dữ liệu hủy) TRƯỚC KHI ngắt kết nối HID
            if (_hidService.IsConnected) // Chỉ gửi nếu vẫn đang kết nối
            {
                SendCanDisableMessage();
            }

            // BƯỚC 2: Hủy gắn handler nhận frame TRƯỚC KHI ngắt kết nối HID stream
            if (_isFrameHandlerAttached)
            {
                _hidService.FrameReceived -= OnFrameReceived;
                _isFrameHandlerAttached = false;
                Console.WriteLine("✅ FrameReceived handler đã được gỡ.");
            }

            // BƯỚC 3: Ngắt kết nối thiết bị HID (sẽ đóng stream)
            _hidService.Disconnect();
            IsConnected = false;
            System.Diagnostics.Debug.WriteLine("CAN đã ngắt kết nối.");
        }



        private void OnFrameReceived(byte[] data)
        {
            Console.WriteLine($"🟢 Frame Received Handler called at {DateTime.Now:HH:mm:ss.fff}");

            if (data == null || data.Length < 2)
                return;

            byte cmd = data[0];
            Console.WriteLine($"🔍 FrameReceived invoked, CMD: {cmd:X2}");

            if (cmd != 0x03)
                return;

            // Byte 1: DLC (4 bit high), Frame Type (4 bit low)
            byte rawInfo = data[1];
            byte dlc = (byte)((rawInfo >> 4) & 0x0F);
            byte frameType = (byte)(rawInfo & 0x0F); // 0: STD, 1: EXT , 2: REMOTE

            // ID: 4 bytes
            if (data.Length < 6 + dlc)
            {
                System.Diagnostics.Debug.WriteLine("❌ Not enough data for full frame.");
                return;
            }

            uint canId = ((uint)data[2] << 24) | ((uint)data[3] << 16) | ((uint)data[4] << 8) | data[5];

            // Payload
            byte[] payload = new byte[dlc];
            Array.Copy(data, 6, payload, 0, dlc);

            // Chuẩn hóa lại ID dạng chuỗi
            string idFormatted = (frameType == 1)
                ? $"0x{canId:X8}" // extended
                : $"0x{(canId & 0x7FF):X3}"; // standard: 11-bit mask

            // Tạo đối tượng CanFrame mới
            var newFrame = new CanFrame
            {
                Timestamp = DateTime.Now,
                CanId = idFormatted,
                Dlc = dlc,
                DataBytesHex = new ObservableCollection<BindableByte>(
                    payload.Select(b => new BindableByte { Value = b.ToString("X2") })
                )
            };

            // Thêm frame vào buffer thay vì cập nhật UI trực tiếp
            _frameBuffer.Enqueue(newFrame);
        }

        // Phương thức xử lý sự kiện Tick của DispatcherTimer
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            int framesAddedThisTick = 0;

            try
            {
                // Di chuyển các frame từ buffer vào ObservableCollection trên UI thread
                // Giới hạn số lượng frame thêm vào mỗi lần tick để tránh làm nghẽn UI
                // Thay thế TryDequeue bằng kiểm tra Count và Dequeue để tương thích với các phiên bản .NET cũ hơn
                while (_frameBuffer.Count > 0 && framesAddedThisTick < MAX_FRAMES_PER_UPDATE)
                {
                    var frame = _frameBuffer.Dequeue(); // Lấy phần tử đầu tiên
                    ReceivedFrames.Add(frame);
                    framesAddedThisTick++;
                }

                // Gọi logic cuộn tự động chỉ khi có frame mới được thêm vào
                // và chỉ một lần mỗi tick để tránh re-rendering liên tục
                if (framesAddedThisTick > 0)
                {
                    ScrollToLatestFrame?.Invoke();
                }
            }
            catch (Exception ex)
            {
                // Log lỗi hoặc hiển thị thông báo lỗi nếu có vấn đề xảy ra trong quá trình cập nhật UI
                System.Diagnostics.Debug.WriteLine($"Error in UiUpdateTimer_Tick: {ex.Message}");
                // Tùy chọn: Nếu lỗi nghiêm trọng, có thể dừng timer
                // _uiUpdateTimer.Stop();
            }
        }
        private const byte HID_OUTPUT_REPORT_ID = 0x00;
        // --- Hàm gửi bản tin cấu hình CAN (header 0x01) ---
        private void SendCanConfigMessage()
        {
            if (!_hidService.IsConnected) // Chỉ gửi nếu đã kết nối
            {
                System.Diagnostics.Debug.WriteLine("Không thể gửi cấu hình: Dịch vụ HID chưa kết nối.");
                return;
            }

            // Command (1 byte): 0x01 (CAN Protocol Control)
            // Baud Rate (2 bytes): SelectedBaudRate
            // Filter Type (1 byte): 0x00 for Standard, 0x01 for Extended (nếu bạn muốn gửi theo cấu hình IsStandardIdFilter)
            // Filter From (4 bytes): FilterFromId (chuyển đổi từ Hex string sang UInt32)
            // Filter To (4 bytes): FilterToId (chuyển đổi từ Hex string sang UInt32)

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
                    System.Diagnostics.Debug.WriteLine($"Định dạng FilterFromId không hợp lệ: {Config.FilterFromId}");
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
                    System.Diagnostics.Debug.WriteLine($"Định dạng FilterToId không hợp lệ: {Config.FilterToId}");
                }
            }

            // Xây dựng mảng byte của bản tin
            // Lấy kích thước payload mong đợi từ HidCanService
            byte[] configMessage = new byte[_hidService.GetHidReportPayloadSize()];
            Array.Clear(configMessage, 0, configMessage.Length); // Đảm bảo tất cả các byte là 0 ban đầu

            configMessage[0] = 0x01; // Command: CAN Protocol Control

            // Baud Rate (Giả định Little-endian)
            configMessage[1] = (byte)(baudRate & 0xFF);
            configMessage[2] = (byte)((baudRate >> 8) & 0xFF);

            configMessage[3] = filterType;

            // Filter From ID (Giả định Little-endian)
            configMessage[4] = (byte)(filterFromId & 0xFF);
            configMessage[5] = (byte)((filterFromId >> 8) & 0xFF);
            configMessage[6] = (byte)((filterFromId >> 16) & 0xFF);
            configMessage[7] = (byte)((filterFromId >> 24) & 0xFF);

            // Filter To ID (Giả định Little-endian)
            configMessage[8] = (byte)(filterToId & 0xFF);
            configMessage[9] = (byte)((filterToId >> 8) & 0xFF);
            configMessage[10] = (byte)((filterToId >> 16) & 0xFF);
            configMessage[11] = (byte)((filterToId >> 24) & 0xFF);

            _hidService.SendFrame(configMessage, HID_OUTPUT_REPORT_ID); // GỬI VỚI REPORT ID
            System.Diagnostics.Debug.WriteLine("Sent CAN Config message (Payload): " + BitConverter.ToString(configMessage));
        }

        // --- Hàm gửi bản tin hủy CAN (header 0x01, baud 0, filter type 0, id 0) ---
        private void SendCanDisableMessage()
        {
            if (!_hidService.IsConnected) // Chỉ gửi nếu đã kết nối
            {
                System.Diagnostics.Debug.WriteLine("Không thể gửi lệnh tắt: Dịch vụ HID chưa kết nối.");
                return;
            }

            // Bản tin hủy CAN: lệnh 0x01 (CAN Protocol Control) với các byte còn lại là 0
            // Thiết bị sẽ hiểu là "tắt CAN" hoặc "không kết nối"
            byte[] disableMessage = new byte[_hidService.GetHidReportPayloadSize()];
            Array.Clear(disableMessage, 0, disableMessage.Length); // Đảm bảo tất cả các byte là 0
            disableMessage[0] = 0x01; // Command: CAN Protocol Control

            _hidService.SendFrame(disableMessage, HID_OUTPUT_REPORT_ID); // GỬI VỚI REPORT ID
            System.Diagnostics.Debug.WriteLine("Sent CAN Disable message (Payload): " + BitConverter.ToString(disableMessage));
        }


        /*
        // --- Hàm gửi bản tin cấu hình CAN (header 0x01) ---
        private void SendCanConfigMessage()
        {
            // Command (1 byte): 0x01 (CAN Protocol Control)
            // Baud Rate (2 bytes): SelectedBaudRate
            // Filter Type (1 byte): 0x00 for Standard, 0x01 for Extended (nếu bạn muốn gửi theo cấu hình IsStandardIdFilter)
            // Filter From (4 bytes): FilterFromId (chuyển đổi từ Hex string sang UInt32)
            // Filter To (4 bytes): FilterToId (chuyển đổi từ Hex string sang UInt32)

            // Lưu ý: Cần xử lý lỗi chuyển đổi hex string sang UInt32 nếu người dùng nhập sai định dạng.
            // Để đơn giản, tôi sẽ sử dụng TryParse và mặc định về 0 nếu lỗi.

            ushort baudRate = (ushort)Config.SelectedBaudRate;
            byte filterType = Config.IsStandardIdFilter ? (byte)0x00 : (byte)0x01;

            uint filterFromId = 0;
            if (!string.IsNullOrEmpty(Config.FilterFromId))
            {
                try
                {
                    filterFromId = Convert.ToUInt32(Config.FilterFromId.Replace("0x", ""), 16);
                }
                catch { /* Bỏ qua hoặc log lỗi nếu không parse được  }
            }

            uint filterToId = 0;
            if (!string.IsNullOrEmpty(Config.FilterToId))
            {
                try
                {
                    filterToId = Convert.ToUInt32(Config.FilterToId.Replace("0x", ""), 16);
                }
                catch { /* Bỏ qua hoặc log lỗi nếu không parse được  }
            }

            // Xây dựng mảng byte của bản tin
            byte[] configMessage = new byte[64]; // 1 byte CMD + 2 byte Baud + 1 byte Filter Type + 4 byte From + 4 byte To
            configMessage[0] = 0x01; // Command: CAN Protocol Control

            // Baud Rate (Big-endian, hoặc Little-endian tùy vào thiết bị của bạn. Ở đây giả định Little-endian)
            configMessage[1] = (byte)(baudRate & 0xFF);
            configMessage[2] = (byte)((baudRate >> 8) & 0xFF);

            configMessage[3] = filterType;

            // Filter From ID (Little-endian, hoặc Big-endian)
            configMessage[4] = (byte)(filterFromId & 0xFF);
            configMessage[5] = (byte)((filterFromId >> 8) & 0xFF);
            configMessage[6] = (byte)((filterFromId >> 16) & 0xFF);
            configMessage[7] = (byte)((filterFromId >> 24) & 0xFF);

            // Filter To ID (Little-endian, hoặc Big-endian)
            configMessage[8] = (byte)(filterToId & 0xFF);
            configMessage[9] = (byte)((filterToId >> 8) & 0xFF);
            configMessage[10] = (byte)((filterToId >> 16) & 0xFF);
            configMessage[11] = (byte)((filterToId >> 24) & 0xFF);

            _hidService.SendFrame(configMessage, 0x0);
            System.Diagnostics.Debug.WriteLine("Sent CAN Config message: " + BitConverter.ToString(configMessage));
        }
        // --- Hàm gửi bản tin hủy CAN (header 0x01, baud 0, filter type 0, id 0) ---
        private void SendCanDisableMessage()
        {
            // Bản tin hủy CAN có thể là cấu hình Baud Rate về 0 hoặc một giá trị đặc biệt
            // Hoặc một command riêng. Giả sử ta gửi baud = 0 để vô hiệu hóa
            byte[] disableMessage = new byte[64];
            disableMessage[0] = 0x01; // Command: CAN Protocol Control
            // Các byte còn lại sẽ là 0 theo mặc định của mảng byte mới, thể hiện baud rate 0, filter 0, id 0
            // Điều này có nghĩa là thiết bị sẽ hiểu là "tắt CAN" hoặc "không kết nối"

            _hidService.SendFrame(disableMessage, 0x00);
            System.Diagnostics.Debug.WriteLine("Sent CAN Disable message: " + BitConverter.ToString(disableMessage));
        }

*/


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

