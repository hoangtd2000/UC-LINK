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

        public ObservableCollection<string> ReceiveLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SendLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ConnectionLogs { get; } = new ObservableCollection<string>();
        public ObservableCollection<CdcFrame> FramesToSend { get; } = new ObservableCollection<CdcFrame>();
        private readonly StringBuilder _receiveBuilder = new StringBuilder();
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

        /*private void SendFrame(CdcFrame frame)
        {
            _cdcService.Send(frame);

        }*/
        /*private void SendFrame(CdcFrame frame)
        {
            string data = frame.DataString;

            if (AppendCR) data += "\r";
            if (AppendLF) data += "\n";

            byte[] dataBytes;

            if (frame.DataType == "ASCII")
            {
                dataBytes = Encoding.ASCII.GetBytes(data); // ✅ giữ nguyên CR/LF
            }
            else
            {
                dataBytes = Helpers.DataConverter.ConvertToBytes(data, frame.DataType); // Hex / Dec
            }

            _cdcService.SendBytes(dataBytes);
        }
        */
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
        /*
        public void SendFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    string data = line;
                    if (GetAppendCR?.Invoke() == true) data += "\r";
                    if (GetAppendLF?.Invoke() == true) data += "\n";

                    var bytes = Encoding.ASCII.GetBytes(data);
                    _cdcService.SendBytes(bytes);

                    Thread.Sleep(10); // gửi từ từ, tránh nghẽn thiết bị
                }

                LogConnection($"[INFO] Đã gửi file: {Path.GetFileName(filePath)} ({lines.Length} dòng)");
            }
            catch (Exception ex)
            {
                LogConnection($"[ERR] Lỗi gửi file: {ex.Message}");
            }
        }
        */
        /*
        public void SendFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                string fileName = Path.GetFileName(filePath);

                if (ext == ".bin")
                {
                    byte[] binData = File.ReadAllBytes(filePath);
                    _cdcService.SendBytes(binData);
                    LogConnection($"[INFO] Đã gửi file BIN ({binData.Length} bytes)");
                }
                else
                {
                    var lines = File.ReadAllLines(filePath);
                    foreach (var line in lines)
                    {
                        byte[] dataBytes;

                        if (ext == ".hex")
                        {
                            // A1 B2 → [0xA1, 0xB2] + thêm \r\n
                            dataBytes = Helpers.DataConverter.ConvertToBytes(line, "HEX")
                                .Concat(new byte[] { 0x0D, 0x0A }) // \r\n
                                .ToArray();
                        }
                        else if (ext == ".dec")
                        {
                            dataBytes = Helpers.DataConverter.ConvertToBytes(line, "DEC")
                                .Concat(new byte[] { 0x0D, 0x0A })
                                .ToArray();
                        }
                        else // .txt hoặc mặc định → ASCII
                        {
                            string data = line;
                            if (GetAppendCR?.Invoke() == true) data += "\r";
                            if (GetAppendLF?.Invoke() == true) data += "\n";

                            dataBytes = Encoding.ASCII.GetBytes(data);
                        }

                        _cdcService.SendBytes(dataBytes);
                        Thread.Sleep(10);
                    }

                    LogConnection($"[INFO] Đã gửi file {fileName} ({lines.Length} dòng)");
                }
            }
            catch (Exception ex)
            {
                LogConnection($"[ERR] Gửi file thất bại: {ex.Message}");
            }
        }
        */
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

                       /* if (ext == ".hex")
                        {
                            // GỬI DẠNG ASCII — giữ nguyên chuỗi như "A1 B2"
                            if (GetAppendCR?.Invoke() == true) data += "\r";
                            if (GetAppendLF?.Invoke() == true) data += "\n";

                            byte[] asciiBytes = Encoding.ASCII.GetBytes(data);
                            _cdcService.SendBytes(asciiBytes);
                        }*/
                       if (ext == ".hex")
                        {
                            // GỬI DẠNG ASCII — giữ nguyên chuỗi như "A1 B2"
                            // nhưng luôn thêm \r\n, KHÔNG phụ thuộc vào checkbox
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

                        Thread.Sleep(10);
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
