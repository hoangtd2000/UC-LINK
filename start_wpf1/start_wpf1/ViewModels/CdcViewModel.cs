using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.IO.Ports;
using start_wpf1.Models;
using start_wpf1.Service;
using start_wpf1.Helpers;
using System.Text;
using System.Windows.Threading;
using System.Linq;

namespace start_wpf1.ViewModels
{
    public class CdcViewModel : INotifyPropertyChanged
    {
        private readonly CdcService _cdcService;
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private readonly DispatcherTimer _uiUpdateTimer;

        public ObservableCollection<string> ReceiveLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SendLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectionLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<CdcFrame> FramesToSend { get; } = new ObservableCollection<CdcFrame>();
        private readonly StringBuilder _receiveBuilder = new StringBuilder();

        private string _receiveLog;
        public string ReceiveLog
        {
            get => _receiveLog;
            set
            {
                _receiveLog = value;
                OnPropertyChanged();
            }
        }
        private string _selectedDisplayMode = "ASCII";
        public string SelectedDisplayMode
        {
            get => _selectedDisplayMode;
            set
            {
                _selectedDisplayMode = value;
                OnPropertyChanged();
            }
        }
        public ICommand SendCommand { get; }

        public event Action AutoScrollRequest;

        public CdcViewModel(CdcService service)
        {
            _cdcService = service ?? throw new ArgumentNullException(nameof(service));
            _cdcService.DataReceived += OnDataReceived;

            SendCommand = new RelayCommand<CdcFrame>(SendFrame);

            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20) // Update UI mỗi 20ms
            };
            _uiUpdateTimer.Tick += (s, e) =>
            {
                if (_receiveBuffer.Length > 0)
                {
                    string raw = _receiveBuffer.ToString();
                    _receiveBuffer.Clear();

                    byte[] bytes = Encoding.ASCII.GetBytes(raw);
                    string converted = raw;

                    switch (SelectedDisplayMode)
                    {
                        case "Hex":
                            converted = string.Join(" ", bytes.Select(b => b.ToString("X2")));
                            break;
                        case "Dec":
                            converted = string.Join(" ", bytes.Select(b => b.ToString()));
                            break;
                        default:
                            converted = raw;
                            break;
                    }

                    ReceiveLog += converted;
                    AutoScrollRequest?.Invoke();
                }
            };
            _uiUpdateTimer.Start();
        }
        
        public void OpenSerial(string port, int baud, Parity parity, int dataBits, StopBits stopBits)
        {
            _cdcService.Open(port, baud, parity, dataBits, stopBits);
        }

        public void CloseSerial()
        {
            _cdcService.Close();
        }

        private void SendFrame(CdcFrame frame)
        {
            _cdcService.Send(frame);
        }

        private void OnDataReceived(string data)
        {
            _receiveBuffer.Append(data); // Ghi vào bộ nhớ tạm, chưa update UI
        }




    public void ClearReceive()
        {
            _receiveBuffer.Clear();       // Xóa tạm
            ReceiveLog = string.Empty;    // Xóa UI
            _cdcService.ClearBuffer();    // Clear buffer SerialPort
        }



        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
