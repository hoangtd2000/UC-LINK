
/*

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


namespace start_wpf1.ViewModels
{
    public class CanViewModel : INotifyPropertyChanged
    {
        private readonly HidCanService _hidService;
        private Dictionary<CanFrame, DispatcherTimer> _cyclicSenders = new Dictionary<CanFrame, DispatcherTimer>();
        public Action ScrollToLatestFrame { get; set; }


        public ObservableCollection<CanFrame> ReceivedFrames { get; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<CanFrame> CanFrames { get; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<byte> DlcOptions { get; } = new ObservableCollection<byte>(Enumerable.Range(0, 9).Select(i => (byte)i));



        public ICommand ConnectCanCommand { get; }
        public ICommand DisconnectCanCommand { get; }
        public ICommand SendCanFrameCommand { get; }


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

        public string TestText => "Hello from CanViewModel";

        public CanViewModel()
        {
            _hidService = new HidCanService();

            ConnectCanCommand = new RelayCommand(ConnectCan);
            DisconnectCanCommand = new RelayCommand(DisconnectCan);
            SendCanFrameCommand = new RelayCommand<CanFrame>(SendCanFrame);

            
        }
        /*

        private void ConnectCan()
        {
            bool connected = _hidService.Connect();

            if (connected)
            {
                // 🔐 Gán handler đúng lúc
                _hidService.FrameReceived -= OnFrameReceived;
                _hidService.FrameReceived += OnFrameReceived;

                IsConnected = true;
                Console.WriteLine("✅ Đã kết nối CAN và gán lại OnFrameReceived");
            }
            else
            {
                IsConnected = false;
                Console.WriteLine("❌ Kết nối CAN thất bại");
            }
        }


        private void DisconnectCan()
        {
            _hidService.FrameReceived -= OnFrameReceived;
            _hidService.Disconnect();
            IsConnected = false;

            Console.WriteLine("🛑 Ngắt kết nối CAN và xóa sự kiện nhận");
        }
        */

/*
private bool _isFrameHandlerAttached = false;

private void ConnectCan()
{
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
    }
    else
    {
        IsConnected = false;
    }
}

private void DisconnectCan()
{
    if (_isFrameHandlerAttached)
    {
        _hidService.FrameReceived -= OnFrameReceived;
        _isFrameHandlerAttached = false;
    }

    _hidService.Disconnect();
    IsConnected = false;
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
    byte frameType = (byte)(rawInfo & 0x0F); // 0: STD, 1: EXT

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

    // GỘP CẢ HAI LẦN GỌI App.Current.Dispatcher.Invoke VÀO MỘT LẦN DUY NHẤT
    App.Current.Dispatcher.Invoke(() =>
    {
        ReceivedFrames.Add(new CanFrame
        {
            Timestamp = DateTime.Now,
            CanId = idFormatted,
            Dlc = dlc,
            DataBytesHex = new ObservableCollection<BindableByte>(
                payload.Select(b => new BindableByte { Value = b.ToString("X2") })
            )
        });

        // 🔽 Auto scroll logic
        ScrollToLatestFrame?.Invoke();
    });
}
/*
private bool _isFrameHandlerAttached = false;

private void ConnectCan()
{
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
    }
    else
    {
        IsConnected = false;
    }
}

private void DisconnectCan()
{
    if (_isFrameHandlerAttached)
    {
        _hidService.FrameReceived -= OnFrameReceived;
        _isFrameHandlerAttached = false;
    }

    _hidService.Disconnect();
    IsConnected = false;
}


private void OnFrameReceived(byte[] data)
{
    /* Console.WriteLine($"🟢 Frame Received Handler called at {DateTime.Now:HH:mm:ss.fff}");
     if (data == null || data.Length < 6)
     {
         System.Diagnostics.Debug.WriteLine($"❌ Frame too short: {data?.Length ?? 0} bytes");
         return;
     }

     // Check header
     if (data[0] != 0x03)
     {
         System.Diagnostics.Debug.WriteLine($"⚠️ Unknown CMD: {data[0]:X2}");
         return;
     }*/
/*
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
byte frameType = (byte)(rawInfo & 0x0F); // 0: STD, 1: EXT

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

// Add to collection (UI Thread)
App.Current.Dispatcher.Invoke(() =>
{
   ReceivedFrames.Add(new CanFrame
   {
       Timestamp = DateTime.Now,
       CanId = idFormatted,
       Dlc = dlc,
       DataBytesHex = new ObservableCollection<BindableByte>(
           payload.Select(b => new BindableByte { Value = b.ToString("X2") })
       )
   });
});
// Add to collection (UI Thread)
App.Current.Dispatcher.Invoke(() =>
{
   ReceivedFrames.Add(new CanFrame
   {
       Timestamp = DateTime.Now,
       CanId = idFormatted,
       Dlc = dlc,
       DataBytesHex = new ObservableCollection<BindableByte>(
           payload.Select(b => new BindableByte { Value = b.ToString("X2") })
       )
   });

   // 🔽 Auto scroll logic
   ScrollToLatestFrame?.Invoke();
});
}
*/
/*
public event PropertyChangedEventHandler PropertyChanged;
protected void OnPropertyChanged([CallerMemberName] string name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
}
*/
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
        public ObservableCollection<byte> DlcOptions { get; } = new ObservableCollection<byte>(Enumerable.Range(0, 0x0F + 1).Select(i => (byte)i)); // DLC có thể từ 0-15 cho CAN FD


        public ICommand ConnectCanCommand { get; }
        public ICommand DisconnectCanCommand { get; }
        public ICommand SendCanFrameCommand { get; }


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

        public string TestText => "Hello from CanViewModel";

        public CanViewModel()
        {
            _hidService = new HidCanService();

            ConnectCanCommand = new RelayCommand(ConnectCan);
            DisconnectCanCommand = new RelayCommand(DisconnectCan);
            SendCanFrameCommand = new RelayCommand<CanFrame>(SendCanFrame);

            // Khởi tạo DispatcherTimer cho việc cập nhật UI
            _uiUpdateTimer = new DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(UI_UPDATE_INTERVAL_MS);
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
        }

        private bool _isFrameHandlerAttached = false;

        private void ConnectCan()
        {
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
            byte frameType = (byte)(rawInfo & 0x0F); // 0: STD, 1: EXT

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

