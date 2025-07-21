/*
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
using System.Threading;
using System.IO;

namespace start_wpf1.ViewModels
{
    public class CdcViewModel : INotifyPropertyChanged
    {
        private readonly CdcService _cdcService;
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private readonly DispatcherTimer _uiUpdateTimer;
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
                }
            }
        }
        //public ObservableCollection<string> ReceiveLogs { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> SendLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectionLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<CdcFrame> FramesToSend { get; } = new ObservableCollection<CdcFrame>();
        public bool AppendCR { get; set; }
        public bool AppendLF { get; set; }
        public Func<bool> GetAppendCR { get; set; }
        public Func<bool> GetAppendLF { get; set; }

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
                   // Console.WriteLine($"[DEBUG-RECEIVE] Processed: {converted.Length} chars");
                }
            };
            _uiUpdateTimer.Start();
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

        private void SendFrame(CdcFrame frame)
        {
            // Lấy trạng thái từ UI
            bool useCR = GetAppendCR?.Invoke() == true;
            bool useLF = GetAppendLF?.Invoke() == true;

            string data = frame.DataString;

            if (useCR) data += "\r";
            if (useLF) data += "\n";

            byte[] dataBytes;

            if (frame.DataType == "ASCII")
            {
                dataBytes = Encoding.ASCII.GetBytes(data);
            }
            else
            {
                dataBytes = Helpers.DataConverter.ConvertToBytes(data, frame.DataType);
            }

            _cdcService.SendBytes(dataBytes);
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

        private void LogConnection(string message)
        {
            ConnectionLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
        public void SendFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                string fileName = Path.GetFileName(filePath);

                if (ext == ".bin")
                {
                    // Đọc file bin → convert sang chuỗi hex → gửi dạng ASCII
                    byte[] raw = File.ReadAllBytes(filePath);
                    string hexString = string.Join(" ", raw.Select(b => b.ToString("X2")));

                    if (GetAppendCR?.Invoke() == true) hexString += "\r";
                    if (GetAppendLF?.Invoke() == true) hexString += "\n";

                    byte[] asciiBytes = Encoding.ASCII.GetBytes(hexString);
                    _cdcService.SendBytes(asciiBytes);

                    LogConnection($"[INFO] Đã gửi file BIN ({raw.Length} bytes dưới dạng chuỗi Hex)");
                }
                else
                {
                    // Gửi từng dòng
                    var lines = File.ReadAllLines(filePath);

                    foreach (var line in lines)
                    {
                        string data = line;
                       if (ext == ".hex")
                        {
                            string dataWithCrLf = line + "\r\n";
                            byte[] asciiBytes = Encoding.ASCII.GetBytes(dataWithCrLf);
                            _cdcService.SendBytes(asciiBytes);
                        }
                        else if (ext == ".dec")
                        {
                            // Convert DEC sang bytes rồi gửi (thêm \r\n)
                            byte[] decBytes = Helpers.DataConverter.ConvertToBytes(line, "DEC")
                                .Concat(new byte[] { 0x0D, 0x0A }) // CRLF cố định
                                .ToArray();

                            _cdcService.SendBytes(decBytes);
                        }
                        else // .txt hoặc không rõ
                        {
                            if (GetAppendCR?.Invoke() == true) data += "\r";
                            if (GetAppendLF?.Invoke() == true) data += "\n";

                            byte[] asciiBytes = Encoding.ASCII.GetBytes(data);
                            _cdcService.SendBytes(asciiBytes);
                        }

                        //Thread.Sleep(1);
                    }

                    LogConnection($"[INFO] Đã gửi file {fileName} ({lines.Length} dòng)");
                }
            }
            catch (Exception ex)
            {
                LogConnection($"[ERR] Gửi file thất bại: {ex.Message}");
            }
        }


        public void SaveLogToFile(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, ReceiveLog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] Lỗi lưu log: {ex.Message}");
            }
        }

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
using System.Text;
using System.Windows.Threading;
using System.Linq;
using System.IO.Ports;
using System.IO;
using start_wpf1.Models;
using start_wpf1.Service;
using System.Collections.Generic;
using System.Threading;
using start_wpf1.Helpers;

namespace start_wpf1.ViewModels
{
    public class CdcViewModel : INotifyPropertyChanged
    {
        private readonly CdcService _cdcService;
        private readonly Queue<string> _lineQueue = new Queue<string>();
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly object _queueLock = new object();
        private const int MaxLines = 10000;

        public ObservableCollection<string> ReceiveLines { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SendLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectionLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<CdcFrame> FramesToSend { get; } = new ObservableCollection<CdcFrame>();

        public bool AppendCR { get; set; }
        public bool AppendLF { get; set; }
        public Func<bool> GetAppendCR { get; set; }
        public Func<bool> GetAppendLF { get; set; }

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
                }
            }
        }

        public CdcViewModel(CdcService service)
        {
            _cdcService = service ?? throw new ArgumentNullException(nameof(service));
            _cdcService.DataReceived += OnDataReceived;

            SendCommand = new RelayCommand<CdcFrame>(SendFrame);

            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(10)
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            _uiUpdateTimer.Start();
        }

        private void OnDataReceived(string data)
        {
            lock (_queueLock)
            {
                _lineQueue.Enqueue(data);
            }
        }
        private string FormatLine(string line)
        {
            if (SelectedDisplayMode == "Hex")
            {
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString("X2")));
            }
            else if (SelectedDisplayMode == "Dec")
            {
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString()));
            }
            else
            {
                return line;
            }
        }
        private void AddToReceiveLines(string line)
        {
            ReceiveLines.Add(line);
            if (ReceiveLines.Count > MaxLines)
                ReceiveLines.RemoveAt(0);
        }
        private string _incompleteLine = string.Empty;
        private DateTime _lastLiveFlush = DateTime.MinValue;
        private int _lastLiveLineIndex = -1;
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            List<string> newLines = new List<string>();

            lock (_queueLock)
            {
                while (_lineQueue.Count > 0)
                    newLines.Add(_lineQueue.Dequeue());
            }

            if (newLines.Count == 0)
                return;

            string raw = string.Concat(newLines);
            string allData = _incompleteLine + raw;

            string[] lines = allData.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            bool endsWithNewline = allData.EndsWith("\n") || allData.EndsWith("\r");

            if (!endsWithNewline)
            {
                _incompleteLine = lines.Last(); // dòng cuối chưa hoàn chỉnh
                lines = lines.Take(lines.Length - 1).ToArray();
            }
            else
            {
                _incompleteLine = string.Empty;
            }

            foreach (var line in lines)
            {
                string formatted = FormatLine(line);
                AddToReceiveLines(formatted);
            }

            // 👇 Nếu có dữ liệu chưa có dấu dòng và đã 500ms chưa hiển thị, hiển thị tạm
            if (!string.IsNullOrEmpty(_incompleteLine))
            {
                string formatted = FormatLine(_incompleteLine);

                if (_lastLiveLineIndex >= 0 && _lastLiveLineIndex < ReceiveLines.Count)
                {
                    ReceiveLines[_lastLiveLineIndex] = formatted; // cập nhật dòng tạm cũ
                }
                else
                {
                    ReceiveLines.Add(formatted);
                    _lastLiveLineIndex = ReceiveLines.Count - 1;
                }

                if (ReceiveLines.Count > MaxLines)
                    ReceiveLines.RemoveAt(0);

                _lastLiveFlush = DateTime.Now;
            }

            AutoScrollRequest?.Invoke();
        }

        /*
        private string FormatLine(string line)
        {
            if (SelectedDisplayMode == "Hex")
            {
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString("X2")));
            }
            else if (SelectedDisplayMode == "Dec")
            {
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString()));
            }
            else
            {
                return line;
            }
        }
        private void AddToReceiveLines(string line)
    {
        ReceiveLines.Add(line);
        if (ReceiveLines.Count > MaxLines)
            ReceiveLines.RemoveAt(0);
    }
    private string _incompleteLine = string.Empty;
        private DateTime _lastLiveFlush = DateTime.MinValue;
        private int _lastLiveLineIndex = -1;

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            List<string> newLines = new List<string>();

            lock (_queueLock)
            {
                while (_lineQueue.Count > 0)
                    newLines.Add(_lineQueue.Dequeue());
            }

            if (newLines.Count == 0)
                return;

            string raw = string.Concat(newLines);
            string allData = _incompleteLine + raw;

            string[] lines = allData.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            bool endsWithNewline = allData.EndsWith("\n") || allData.EndsWith("\r");

            if (!endsWithNewline)
            {
                _incompleteLine = lines.Last(); // dòng cuối chưa hoàn chỉnh
                lines = lines.Take(lines.Length - 1).ToArray();
            }
            else
            {
                _incompleteLine = string.Empty;
            }

            foreach (var line in lines)
            {
                string formatted = FormatLine(line);
                AddToReceiveLines(formatted);
            }


            if (!string.IsNullOrEmpty(_incompleteLine))
            {
                string formatted = FormatLine(_incompleteLine );

                if (_lastLiveLineIndex >= 0 && _lastLiveLineIndex < ReceiveLines.Count)
                {
                    ReceiveLines[_lastLiveLineIndex] = formatted; // cập nhật dòng tạm cũ
                }
                else
                {
                    ReceiveLines.Add(formatted);
                    _lastLiveLineIndex = ReceiveLines.Count - 1;
                }

                if (ReceiveLines.Count > MaxLines)
                    ReceiveLines.RemoveAt(0);

                _lastLiveFlush = DateTime.Now;
            }

            AutoScrollRequest?.Invoke();
        }

        


 
        // Chúng ta không cần _lastLiveLineIndex hay _lastLiveFlush nữa,
        // thay vào đó dùng một cờ đơn giản hơn.
       /* private bool _isCurrentLastLineIncomplete = false; // Cờ mới để theo dõi xem dòng cuối cùng có phải là dòng chưa hoàn chỉnh tạm thời hay không

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            List<string> newLines = new List<string>();

            lock (_queueLock)
            {
                while (_lineQueue.Count > 0)
                    newLines.Add(_lineQueue.Dequeue());
            }

            // Nếu không có dữ liệu mới và cũng không có dòng dang dở, thì không làm gì cả.
            if (newLines.Count == 0 && string.IsNullOrEmpty(_incompleteLine))
                return;

            string raw = string.Concat(newLines);
            string allData = _incompleteLine + raw; // Nối dữ liệu dang dở từ lần trước với dữ liệu mới

            string[] lines = allData.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            bool endsWithNewline = allData.EndsWith("\n") || allData.EndsWith("\r");

            if (!endsWithNewline)
            {
                _incompleteLine = lines.Last(); // Dòng cuối cùng chưa hoàn chỉnh
                lines = lines.Take(lines.Length - 1).ToArray(); // Lấy tất cả các dòng hoàn chỉnh
            }
            else
            {
                _incompleteLine = string.Empty; // Dữ liệu kết thúc bằng newline, không có dòng dang dở
            }

            // --- Bắt đầu logic cập nhật ReceiveLines ---

            // 1. Nếu dòng cuối cùng hiện tại là một dòng chưa hoàn chỉnh tạm thời từ lần trước, hãy xóa nó.
            if (_isCurrentLastLineIncomplete && ReceiveLines.Count > 0)
            {
                ReceiveLines.RemoveAt(ReceiveLines.Count - 1);
                _isCurrentLastLineIncomplete = false; // Đặt lại cờ
            }

            // 2. Thêm tất cả các dòng hoàn chỉnh mới vào danh sách.
            foreach (var line in lines)
            {
                string formatted = FormatLine(line);
                AddToReceiveLines(formatted); // AddToReceiveLines sẽ tự động xử lý MaxLines
            }

            // 3. Nếu có một dòng chưa hoàn chỉnh mới hoặc cập nhật, thêm nó vào cuối danh sách.
            if (!string.IsNullOrEmpty(_incompleteLine))
            {
                string formatted = FormatLine(_incompleteLine);
                AddToReceiveLines(formatted); // Thêm dòng dang dở tạm thời
                _isCurrentLastLineIncomplete = true; // Đánh dấu rằng dòng cuối cùng hiện tại là dòng tạm thời
            }

            // --- Kết thúc logic cập nhật ReceiveLines ---

            AutoScrollRequest?.Invoke();
        }
        */

        private void SendFrame(CdcFrame frame)
        {
            bool useCR = GetAppendCR?.Invoke() == true;
            bool useLF = GetAppendLF?.Invoke() == true;

            string data = frame.DataString;
            if (useCR) data += "\r";
            if (useLF) data += "\n";

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
            _lastLiveLineIndex = -1;

            ReceiveLines.Clear();
            _cdcService.ClearBuffer(); // nếu bạn có hỗ trợ clear trong service
        }




        public void SaveLogToFile(string filePath)
        {
            try
            {
                File.WriteAllLines(filePath, ReceiveLines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] Lỗi lưu log: {ex.Message}");
            }
        }

        private void LogConnection(string msg)
        {
            ConnectionLogs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
        
        public void SendFile(string filePath)
        {
            _cdcService.SendFile(filePath, GetAppendCR?.Invoke() == true, GetAppendLF?.Invoke() == true, LogConnection);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
