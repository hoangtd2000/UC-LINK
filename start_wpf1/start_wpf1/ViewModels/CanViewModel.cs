using start_wpf1.Models;
using start_wpf1.Service;
using start_wpf1.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Threading;

namespace start_wpf1.ViewModels
{
    public class CanViewModel : INotifyPropertyChanged
    {
        private readonly HidCanService _hidService = new HidCanService();
        private Timer _cycleTimer;

        

        public ObservableCollection<CanFrame> CanFrames { get; set; } = new ObservableCollection<CanFrame>();
        public ObservableCollection<CanFrame> ReceivedFrames { get; set; } = new ObservableCollection<CanFrame>();

        public ICommand ConnectCanCommand { get; }
        public ICommand DisconnectCanCommand { get; }

        public CanViewModel()
        {
            ConnectCanCommand = new RelayCommand(ConnectCan);
            DisconnectCanCommand = new RelayCommand(DisconnectCan);

            _hidService.DataReceived += OnHidDataReceived;

            _cycleTimer = new Timer(SendCyclicFrames, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void ConnectCan()
        {
            bool success = _hidService.Connect();
            Console.WriteLine(success ? "Kết nối thành công." : "Kết nối thất bại.");
            if (success)
            {
                _cycleTimer.Change(0, 100); // gửi mỗi 100ms
            }
        }

        private void DisconnectCan()
        {
            _cycleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _hidService.Disconnect();
        }

        public void SendCanFrame(CanFrame frame)
        {
            var bytes = ConvertCanFrameToBytes(frame);
            _hidService.Send(bytes);
        }

        private void SendCyclicFrames(object state)
        {
            foreach (var frame in CanFrames.Where(f => f.IsCyclic))
            {
                var bytes = ConvertCanFrameToBytes(frame);
                _hidService.Send(bytes);
            }
        }

        private void OnHidDataReceived(byte[] data)
        {
            string hex = BitConverter.ToString(data).Replace("-", " ");

            App.Current.Dispatcher.Invoke(() =>
            {
                ReceivedFrames.Add(new CanFrame
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    CanId = "0x000", // chưa parse ID
                    Dlc = data.Length,
                    DataHex = hex
                });
            });
        }

        private byte[] ConvertCanFrameToBytes(CanFrame frame)
        {
            byte[] idBytes = BitConverter.GetBytes(Convert.ToInt32(frame.CanId, 16));
            byte[] dataBytes = DataConverter.ConvertToBytes(frame.DataHex, "HEX");
            byte dlc = (byte)(frame.Dlc & 0x0F);

            byte[] packet = new byte[13]; // ví dụ
            packet[0] = 0xA1;
            Array.Copy(idBytes, 0, packet, 1, 4);
            packet[5] = dlc;
            Array.Copy(dataBytes, 0, packet, 6, Math.Min(8, dataBytes.Length));

            return packet;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
