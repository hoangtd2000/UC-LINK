using HidSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace UsbComposite.Service
{
    public class CanService : IDisposable
    {
        private const int VendorId = 0x0078;
        private const int ProductId = 0x2000;
        private const int PacketSize = 32;
        private const int HID_REPORT_PAYLOAD_SIZE = 32; // Đổi tên để rõ ràng hơn là kích thước payload

        private HidDevice _device;
        private HidStream _stream;
        private CancellationTokenSource _cts;

        public bool IsConnected => _stream != null && _stream.CanWrite;

        public event Action<byte[]> FrameReceived;

        public event Action Disconnected;
        
        public int GetHidReportPayloadSize()
        {
            return HID_REPORT_PAYLOAD_SIZE; 
        }
        

        public bool Connect()
        {
            var list = DeviceList.Local;
            _device = list.GetHidDevices(VendorId, ProductId).FirstOrDefault();

            if (_device == null)
                return false;

            if (_device.TryOpen(out _stream))
            {
                _cts = new CancellationTokenSource();
                StartListening(_cts.Token);
                return true;
            }

            return false;
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;
        }
        // THAY ĐỔI LỚN NHẤT: Thêm tham số reportId
        public bool SendFrame(byte[] data, byte reportId)
        {
            if (!IsConnected || data == null) return false;

            // Kích thước của gói HID để gửi là kích thước payload + 1 byte cho Report ID
            var packet = new byte[HID_REPORT_PAYLOAD_SIZE + 1];

            // Gán Report ID vào byte đầu tiên
            packet[0] = reportId;

            // Sao chép dữ liệu vào từ byte thứ 1
            // Đảm bảo không sao chép quá kích thước payload
            Array.Copy(data, 0, packet, 1, Math.Min(data.Length, HID_REPORT_PAYLOAD_SIZE));

            try
            {
                _stream.Write(packet, 0, packet.Length);
                //System.Diagnostics.Debug.WriteLine($"Sent HID report (Report ID: {reportId:X2}): {BitConverter.ToString(packet)}");
                return true;
            }
            
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"IO Exception: {ex.Message}");
                Disconnected?.Invoke();
                return false;
            }
        }

        public bool SendFrame(byte[] data)
        {
            if (!IsConnected || data == null) return false;

            var packet = new byte[PacketSize + 1]; // 1 byte cho Report ID

            Array.Copy(data, 0, packet, 1, Math.Min(data.Length, PacketSize));

            try
            {
                _stream.Write(packet, 0, packet.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void StartListening(CancellationToken token)
        {
            Task.Run(async () =>
            {
                var buffer = new byte[PacketSize + 1];

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = await _stream.ReadAsync(buffer, 0, PacketSize, token);

                        if (bytesRead > 0)
                        {
                            // Skip the first byte (Report ID)
                            byte[] received = new byte[bytesRead - 1];
                            Array.Copy(buffer, 1, received, 0, bytesRead - 1);

                            FrameReceived?.Invoke(received);
                          //  Console.WriteLine($"✅ FrameReceived invoked, CMD: {received[0]:X2}");
                        }
                        else
                        {
                            // Không đọc được gì, có thể thiết bị ngắt kết nối
                            // System.Diagnostics.Debug.WriteLine("⚠️ No data received, possible disconnect.");
                        }
                    }
                    catch (IOException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ IO Exception: {ex.Message}");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❗ Unexpected error: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("🛑 Stopped listening");
            }, token);
        }


        public void Dispose()
        {
            Disconnect();
        }
    }
}
