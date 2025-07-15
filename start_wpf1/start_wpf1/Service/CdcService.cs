using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using start_wpf1.Models;
using System.Linq;

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
            try
            {
                if (_port != null && _port.IsOpen)
                {
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();
                    Console.WriteLine("[CDC] Clear buffer cổng COM");
                }
                else
                {
                    Console.WriteLine("[CDC] Không thể xóa buffer: COM chưa mở");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CDC] Lỗi khi clear buffer: {ex.Message}");
            }
        }


        public void Open(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            try
            {
                Close(); // đảm bảo đóng trước

                _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    Encoding = Encoding.ASCII,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

                _port.DataReceived += OnDataReceived;
                _port.Open();
                LogConnection($"[OPEN] {portName} @ {baudRate}bps");
            }
            catch (Exception ex)
            {
                LogConnection($"[ERROR] Opencom FAILED: {ex.Message}");
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

                    LogConnection("[CLOSE] Comport closed");
                }
            }
            catch (Exception ex)
            {
                LogConnection($"[ERROR] Close comport FAILED: {ex.Message}");
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
        public void SendBytes(byte[] data)
        {
            try
            {
                if (!IsOpen)
                {
                    LogConnection("[WARN] Cổng COM chưa mở.");
                    return;
                }

                _port.Write(data, 0, data.Length);
                string hex = string.Join(" ", data.Select(b => b.ToString("X2")));
                Console.WriteLine($"[SEND] Bytes: {hex}");

                string preview = Encoding.ASCII.GetString(data);
                Console.WriteLine($"[SEND] String: {preview}");

                _syncContext?.Post(_ => SentData?.Invoke(preview), null);
            }
            catch (Exception ex)
            {
                LogConnection($"[ERR] Gửi bytes thất bại: {ex.Message}");
            }
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
