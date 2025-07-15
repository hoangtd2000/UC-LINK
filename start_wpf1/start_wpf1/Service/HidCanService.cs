using HidSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace start_wpf1.Service
{
    public class HidCanService
    {
        private HidDevice _device;
        private HidStream _stream;
        private CancellationTokenSource _cts;

        public event Action<byte[]> DataReceived;

        public bool IsConnected => _stream != null && _stream.CanRead;

        public bool Connect()
        {
            try
            {
                var list = DeviceList.Local;
                // Gắn cố định VID/PID ở đây
                _device = list.GetHidDevices(0x0078, 0x2000).FirstOrDefault();

                if (_device == null)
                {
                    Console.WriteLine("❌ Không tìm thấy thiết bị HID.");
                    return false;
                }

                if (_device.TryOpen(out _stream))
                {
                    _cts = new CancellationTokenSource();
                    Task.Run(() => ReadLoop(_cts.Token));
                    Console.WriteLine("✅ Đã mở HID stream.");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] Kết nối HID lỗi: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _stream = null;
                Console.WriteLine("Đã ngắt kết nối thiết bị HID.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi ngắt kết nối: {ex.Message}");
            }
        }

        private async Task ReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[64];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    int count = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (count > 0)
                    {
                        byte[] data = buffer.Take(count).ToArray();

                        Console.WriteLine("[RX] " + BitConverter.ToString(data)); // kiểm tra log console

                        DataReceived?.Invoke(data); // 🔥 GỌI VỀ ViewModel
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi đọc HID: {ex.Message}");
                    break;
                }
            }
        }


        public void Send(byte[] report)
        {
            try
            {
                if (_stream != null && _stream.CanWrite)
                {
                    if (report.Length < 64)
                    {
                        Array.Resize(ref report, 64); // đảm bảo đủ report size
                    }

                    _stream.Write(report, 0, report.Length);
                    Console.WriteLine("[TX] " + BitConverter.ToString(report));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi gửi HID: {ex.Message}");
            }
        }
    }
}
