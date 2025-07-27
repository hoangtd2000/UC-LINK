using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text;
using System.Windows.Threading;
using System.Linq;
using System.IO.Ports;
using System.IO;
using start_wpf1.Models;
using start_wpf1.Service;
using System.Collections.Generic;
using start_wpf1.Helpers;
using System.Windows;

namespace start_wpf1.ViewModels
{
    public class CdcViewModel : INotifyPropertyChanged
    {
        private readonly CdcService _cdcService;
        private readonly Queue<string> _lineQueue = new Queue<string>();
        private readonly DispatcherTimer _uiUpdateTimer;
        public readonly object _queueLock = new object();

        public event Action<string> NewLinesReceived;

        public ICommand SendFileCommand { get; }
        public ICommand SaveLogCommand { get; }
        public ICommand ClearReceiveCommand { get; }
        public ICommand OpenComCommand { get; }
        public ICommand CloseComCommand { get; }


        public string SelectedPort { get; set; }
        public int SelectedBaudRate { get; set; } = 115200;
        public int SelectedDataBits { get; set; } = 8;
        public Parity SelectedParity { get; set; } = Parity.None;
        public StopBits SelectedStopBits { get; set; } = StopBits.One;

        // Thay đổi: truyền 1 chuỗi lớn để append TextBox

        private const int MaxLines = 10000;

        public ObservableCollection<string> ReceiveLines { get; } = new ObservableCollection<string>();
        private readonly List<string> _fullLog = new List<string>(); // lưu toàn bộ log

        public ObservableCollection<string> SendLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectionLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<CdcFrame> FramesToSend { get; } = new ObservableCollection<CdcFrame>();
        private StringBuilder _receiveBuffer = new StringBuilder();
        private string _incompleteLine = "";  // để lưu phần chưa hoàn chỉnh
        private DispatcherTimer _comScanTimer;

        public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();


        public Func<bool> GetAppendCR { get; set; }
        public Func<bool> GetAppendLF { get; set; }

        private string _selectedDisplayMode = "ASCII";
        public string SelectedDisplayMode
        {
            get => _selectedDisplayMode;
            set { _selectedDisplayMode = value; OnPropertyChanged(); }
        }

        public ICommand SendCommand { get; }

        public event Action AutoScrollRequest;

        private bool _isSerialOpen;
        public bool IsSerialOpen
        {
            get => _isSerialOpen;
            set
            {
                if (_isSerialOpen != value)
                {
                    _isSerialOpen = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();

                    // ✅ Dừng hoặc chạy lại timer quét COM
                    if (_isSerialOpen)
                    {
                        _comScanTimer?.Stop(); // Dừng nếu COM đang mở
                    }
                    else
                    {
                        _comScanTimer?.Start(); // Bắt đầu lại nếu COM đã đóng
                        UpdateAvailablePorts(); // Cập nhật ngay danh sách cổng
                    }
                }
            }
        }
        private bool _appendCR;
        public bool AppendCR
        {
            get => _appendCR;
            set
            {
                _appendCR = value;
                OnPropertyChanged(nameof(AppendCR));
            }
        }

        private bool _appendLF;
        public bool AppendLF
        {
            get => _appendLF;
            set
            {
                _appendLF = value;
                OnPropertyChanged(nameof(AppendLF));
            }
        }

        private string _receiveLog = "";
        public string ReceiveLog
        {
            get => _receiveLog;
            set
            {
                _receiveLog = value;
                OnPropertyChanged(nameof(ReceiveLog));
            }
        }




        public CdcViewModel(CdcService service)
        {
            _cdcService = service ?? throw new ArgumentNullException(nameof(service));
            _cdcService.DataReceived += OnDataReceived;

            SendCommand = new RelayCommand<CdcFrame>(SendFrame);


            SendFileCommand = new RelayCommand(ExecuteSendFile);
            SaveLogCommand = new RelayCommand(ExecuteSaveLog);
            ClearReceiveCommand = new RelayCommand(ExecuteClearReceive);
            OpenComCommand = new RelayCommand(ExecuteOpenCom, CanOpenCom);

            CloseComCommand = new RelayCommand(ExecuteCloseCom, CanCloseCom);


            _comScanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _comScanTimer.Tick += (s, e) =>
            {
                if (!IsSerialOpen)
                    UpdateAvailablePorts();
            };

            // ✅ Chỉ bắt đầu timer nếu chưa mở COM
            if (!IsSerialOpen)
                _comScanTimer.Start();


            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            _uiUpdateTimer.Start();
        }
        private void ExecuteSendFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Files|*.txt;*.hex;*.bin;*.dec|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                SendFile(dialog.FileName);
                LastSentFileName = $"Đã gửi: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }
        // Property này để binding ra View
        private string _lastSentFileName;
        public string LastSentFileName
        {
            get => _lastSentFileName;
            set
            {
                _lastSentFileName = value;
                OnPropertyChanged(nameof(LastSentFileName));
            }
        }


        private void ExecuteSaveLog()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                FileName = $"UC-LINK_cdc_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                SaveLogToFile(dialog.FileName);
            }
        }
        private void ExecuteClearReceive( )
        {
            ClearReceive();

            // Nếu bạn muốn dọn log đang hiển thị trên giao diện
            NewLinesReceived?.Invoke(string.Empty);
        }
        private void ExecuteOpenCom()
        {
            try
            {
                OpenSerial(SelectedPort, SelectedBaudRate, SelectedParity, SelectedDataBits, SelectedStopBits);
                IsSerialOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Open COM error: {ex.Message}");
            }
        }

        private bool CanOpenCom( )
        {
            return !IsSerialOpen;
        }
        private void ExecuteCloseCom( )
        {
            try
            {
                CloseSerial();
                IsSerialOpen = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Close comport ERROR: {ex.Message}");
            }
        }

        private bool CanCloseCom( )
        {
            return IsSerialOpen;
        }

        private void UpdateAvailablePorts()
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();

            // Thêm cổng mới
            foreach (var port in ports)
            {
                if (!AvailablePorts.Contains(port))
                    AvailablePorts.Add(port);
            }

            // Xoá cổng đã biến mất
            for (int i = AvailablePorts.Count - 1; i >= 0; i--)
            {
                if (!ports.Contains(AvailablePorts[i]))
                    AvailablePorts.RemoveAt(i);
            }

            // ✅ Nếu không có COM nào đang chọn và danh sách không trống → chọn COM đầu tiên
            if (string.IsNullOrEmpty(SelectedPort) && AvailablePorts.Count > 0)
            {
                SelectedPort = AvailablePorts[0];
                OnPropertyChanged(nameof(SelectedPort)); // ⚠️ Bắt buộc phải gọi để UI cập nhật
            }
        }


        private void OnDataReceived(string data)
        {
            lock (_queueLock)
            {
                _lineQueue.Enqueue(data);
            }
           // System.Diagnostics.Debug.WriteLine($"Received data: {data}");
        }

        private string FormatLine(string line)
        {
            if (SelectedDisplayMode == "Hex")
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString("X2")));
            if (SelectedDisplayMode == "Dec")
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString()));
            return line;
        }

       
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            List<string> newChunks;

            lock (_queueLock)
            {
                if (_lineQueue.Count == 0)
                    return;

                newChunks = _lineQueue.ToList();
                _lineQueue.Clear();
            }

            // Ghép tất cả chunk thành 1 chuỗi lớn
            string combined = string.Concat(newChunks);

            // Định dạng hiển thị
            string formatted = FormatLine(combined);

            // Cập nhật log và collection
            _fullLog.Add(formatted);

            // Nếu bạn dùng ObservableCollection<string> để hiển thị từng dòng, 
            // bạn có thể thêm từng chunk làm một dòng, hoặc thêm toàn bộ chuỗi một dòng.

            ReceiveLines.Add(formatted);

            // Giới hạn số dòng
            while (ReceiveLines.Count > MaxLines)
                ReceiveLines.RemoveAt(0);

            // Gửi sự kiện cập nhật UI
            NewLinesReceived?.Invoke(formatted);


            AutoScrollRequest?.Invoke();
        }

        public void SendFile(string filePath)
        {
            _cdcService.SendFile(filePath, GetAppendCR?.Invoke() == true, GetAppendLF?.Invoke() == true, LogConnection);
        }

        private void SendFrame(CdcFrame frame)
        {

            string data = frame.DataString;
            if (AppendCR) data += "\r";
            if (AppendLF) data += "\n";

            byte[] dataBytes = frame.DataType == "ASCII"
                ? Encoding.ASCII.GetBytes(data)
                : Helpers.DataConverter.ConvertToBytes(data, frame.DataType);

            _cdcService.SendBytes(dataBytes);
        }

        public void OpenSerial(string port, int baud, Parity parity, int dataBits, StopBits stopBits)
        {
            _cdcService.Open(port, baud, parity, dataBits, stopBits);
            IsSerialOpen = _cdcService.IsOpen;
        }

        public void CloseSerial()
        {
            _cdcService.Close();
            IsSerialOpen = _cdcService.IsOpen;
        }

        public void ClearReceive()
        {
            lock (_queueLock)
            {
                _lineQueue.Clear();
            }

            _incompleteLine = string.Empty;
            _fullLog.Clear();
            ReceiveLines.Clear();

            // Xóa buffer trên cổng COM
            _cdcService.ClearBuffer();

            // Gửi sự kiện cập nhật UI nếu dùng TextBox/TextBlock
            NewLinesReceived?.Invoke(string.Empty);
        }


        public void SaveLogToFile(string filePath)
        {
            try
            {
                File.WriteAllLines(filePath, _fullLog);
            }
            catch (Exception ex)
            {
                ConnectionLogs.Add($"[ERR] Lỗi lưu log: {ex.Message}");
            }
        }


        private void LogConnection(string msg)
        {
            ConnectionLogs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
