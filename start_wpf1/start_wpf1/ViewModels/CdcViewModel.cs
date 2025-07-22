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

namespace start_wpf1.ViewModels
{
    public class CdcViewModel : INotifyPropertyChanged
    {
        private readonly CdcService _cdcService;
        private readonly Queue<string> _lineQueue = new Queue<string>();
        private readonly DispatcherTimer _uiUpdateTimer;
        public readonly object _queueLock = new object();

        public event Action<string> NewLinesReceived;

        // Thay đổi: truyền 1 chuỗi lớn để append TextBox

        private const int MaxLines = 10000;

        public ObservableCollection<string> ReceiveLines { get; } = new ObservableCollection<string>();
        private readonly List<string> _fullLog = new List<string>(); // lưu toàn bộ log

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
                }
            }
        }

        private string _incompleteLine = string.Empty;

        public CdcViewModel(CdcService service)
        {
            _cdcService = service ?? throw new ArgumentNullException(nameof(service));
            _cdcService.DataReceived += OnDataReceived;

            SendCommand = new RelayCommand<CdcFrame>(SendFrame);

            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
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
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString("X2")));
            if (SelectedDisplayMode == "Dec")
                return string.Join(" ", Encoding.ASCII.GetBytes(line).Select(b => b.ToString()));
            return line;
        }
        private int _incompleteLineTickCounter = 0;
        private const int IncompleteLineTimeoutTicks = 2; // hiển thị sau 5 lần tick (~500ms nếu timer 100ms)

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            List<string> newChunks;

            lock (_queueLock)
            {
                if (_lineQueue.Count == 0 && string.IsNullOrEmpty(_incompleteLine))
                    return;

                newChunks = _lineQueue.ToList();
                _lineQueue.Clear();
            }

            string combined = _incompleteLine + string.Concat(newChunks);

            List<string> finalLines = new List<string>();
            int lastNewlinePos = -1;

            for (int i = 0; i < combined.Length; i++)
            {
                if (combined[i] == '\r' || combined[i] == '\n')
                {
                    if (combined[i] == '\r' && i + 1 < combined.Length && combined[i + 1] == '\n')
                        i++; // skip \n in \r\n

                    string line = combined.Substring(lastNewlinePos + 1, i - lastNewlinePos - 1);
                    finalLines.Add(line);
                    lastNewlinePos = i;
                }
            }

            _incompleteLine = (lastNewlinePos + 1 < combined.Length)
                ? combined.Substring(lastNewlinePos + 1)
                : string.Empty;

            // Nếu có dòng hoàn chỉnh => reset timeout
            if (finalLines.Count > 0)
                _incompleteLineTickCounter = 0;
            else if (!string.IsNullOrEmpty(_incompleteLine))
                _incompleteLineTickCounter++;
            else
                _incompleteLineTickCounter = 0;

            // Nếu quá timeout => hiển thị incompleteLine như dòng hoàn chỉnh
            if (_incompleteLineTickCounter >= IncompleteLineTimeoutTicks && !string.IsNullOrEmpty(_incompleteLine))
            {
                finalLines.Add(_incompleteLine);
                _incompleteLine = "";
                _incompleteLineTickCounter = 0;
            }

            foreach (var line in finalLines)
            {
                var formatted = FormatLine(line);
                _fullLog.Add(formatted);
                ReceiveLines.Add(formatted);
            }

            while (ReceiveLines.Count > MaxLines)
                ReceiveLines.RemoveAt(0);

            if (finalLines.Count > 0)
            {
                string combinedText = string.Join(Environment.NewLine, finalLines.Select(FormatLine)) + Environment.NewLine;
                NewLinesReceived?.Invoke(combinedText);
            }

            AutoScrollRequest?.Invoke();
        }






        public void SendFile(string filePath)
        {
            _cdcService.SendFile(filePath, GetAppendCR?.Invoke() == true, GetAppendLF?.Invoke() == true, LogConnection);
        }

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
            _incompleteLineTickCounter = 0;
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
