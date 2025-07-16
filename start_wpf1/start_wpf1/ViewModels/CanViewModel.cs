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
            if (!_hidService.IsConnected || frame == null) return;

            if (frame.IsCyclic)
            {
                // Toggle: nếu đang gửi rồi thì dừng
                if (_cyclicSenders.ContainsKey(frame))
                {
                    _cyclicSenders[frame].Stop();
                    _cyclicSenders.Remove(frame);
                    return;
                }

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(frame.CycleTimeMs)
                };

                timer.Tick += (s, e) =>
                {
                    var bytes = frame.ToBytes();
                    _hidService.SendFrame(bytes);
                };

                _cyclicSenders[frame] = timer;
                timer.Start();
            }
            else
            {
                var bytes = frame.ToBytes();
                _hidService.SendFrame(bytes);
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
            _hidService.FrameReceived += OnFrameReceived;
        }

        private void ConnectCan()
        {
            IsConnected = _hidService.Connect();
        }

        private void DisconnectCan()
        {
            _hidService.Disconnect();
            IsConnected = false;
        }

        private void OnFrameReceived(byte[] data)
        {
            // Giả sử bạn định dạng như sau:
            // [0] = CMD
            // [1..3] = CAN ID (big endian)
            // [4] = DLC
            // [5..12] = data bytes

            // Thêm kiểm tra độ dài tối thiểu cho CMD, CAN ID và DLC
            if (data.Length < 5)
            {
                // Dữ liệu quá ngắn, không thể phân tích
                System.Diagnostics.Debug.WriteLine($"Error: Received frame is too short. Length: {data.Length}. Expected at least 5 bytes.");
                return;
            }

            var canId = (data[1] << 16) | (data[2] << 8) | data[3];
            var dlc = data[4];

            // Kiểm tra xem dữ liệu có đủ để chứa payload theo DLC không
            if (data.Length < 5 + dlc)
            {
                System.Diagnostics.Debug.WriteLine($"Error: Incomplete frame for declared DLC. Expected: {5 + dlc} bytes, Actual: {data.Length} bytes.");
                return;
            }

            byte[] payload = new byte[dlc];
            Array.Copy(data, 5, payload, 0, dlc);

            App.Current.Dispatcher.Invoke(() =>
            {
                ReceivedFrames.Add(new CanFrame
                {
                    Timestamp = DateTime.Now,
                    CanId = $"0x{canId:X3}",
                    Dlc = dlc,
                    // Thay thế dòng lỗi bằng cách gán trực tiếp cho thuộc tính của đối tượng CanFrame mới
                    DataBytesHex = new ObservableCollection<BindableByte>(
                        payload.Select(b => new BindableByte { Value = b.ToString("X2") })
                    )
                });
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}