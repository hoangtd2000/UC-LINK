using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using start_wpf1.Models;


namespace start_wpf1.Service
{
    public class CdcService
    {
        private SerialPort _port;
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private SynchronizationContext _syncContext;

        public event Action<string> DataReceived;
        public event Action<string> SentData;
        public event Action<string> ConnectionLog;

        public bool IsOpen => _port?.IsOpen ?? false;
        public void ClearBuffer()
        {
            _receiveBuffer.Clear();
            _port.DiscardInBuffer();  // clear dữ liệu chưa đọc
            _port.DiscardOutBuffer(); // clear dữ liệu chưa gửi
        }

        public void Open(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            try
            {
                Close(); // đảm bảo đóng trước

                _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    Encoding = Encoding.ASCII,
                   // Encoding = Encoding.UTF8,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

                _port.DataReceived += OnDataReceived;
                _port.Open();
               // Thread.Sleep(50); // Cho thiết bị ổn định
                //_port.DiscardInBuffer();
                LogConnection($"[OPEN] {portName} @ {baudRate}bps");
            }
            catch (Exception ex)
            {
                LogConnection($"[ERROR] Mở cổng thất bại: {ex.Message}");
                throw;
            }
        }

        public void Close()
        {
            try
            {
                if (_port != null)
                {
                    _port.DataReceived -= OnDataReceived;

                    if (_port.IsOpen)
                        _port.Close();

                    LogConnection("[CLOSE] Cổng COM đã đóng");
                }
            }
            catch (Exception ex)
            {
                LogConnection($"[ERROR] Đóng cổng thất bại: {ex.Message}");
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = _port.BytesToRead;
                if (bytesToRead <= 0) return;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _port.Read(buffer, 0, bytesToRead);

                if (bytesRead > 0)
                {
                    string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    DispatchData(data); // Gửi lên ViewModel xử lý
                }
            }
            catch (Exception ex)
            {
                DispatchData($"[ERR] {ex.Message}");
            }
        }





        private void DispatchData(string data)
        {
            _syncContext.Post(_ => DataReceived?.Invoke(data), null);
        }

        private void LogConnection(string msg)
        {
            _syncContext?.Post(_ => ConnectionLog?.Invoke(msg), null);
        }

        public void Send(CdcFrame frame)
        {
            try
            {
                if (!IsOpen)
                {
                    LogConnection("[WARN] Cổng COM chưa mở.");
                    return;
                }

                byte[] dataBytes = Helpers.DataConverter.ConvertToBytes(frame.DataString, frame.DataType);
                _port.Write(dataBytes, 0, dataBytes.Length);

                _syncContext.Post(_ => SentData?.Invoke(frame.DataString), null);
            }
            catch (Exception ex)
            {
                LogConnection($"[ERR] Gửi thất bại: {ex.Message}");
            }
        }
    }
}
