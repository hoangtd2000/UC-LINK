using HidSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace start_wpf1.Service
{
    public class HidCanService : IDisposable
    {
        private const int VendorId = 0x0078;
        private const int ProductId = 0x2000;
        private const int PacketSize = 64;

        private HidDevice _device;
        private HidStream _stream;
        private CancellationTokenSource _cts;

        public bool IsConnected => _stream != null && _stream.CanWrite;

        public event Action<byte[]> FrameReceived;

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

        public bool SendFrame(byte[] data)
        {
            if (!IsConnected || data == null) return false;

            var packet = new byte[PacketSize + 1]; // 1 byte cho Report ID
            //packet[0] = 0x02; // Report ID, hoặc để = 0 nếu không sử dụng

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

        /*
        private void StartListening(CancellationToken token)
        {
            Task.Run(() =>
            {
                var buffer = new byte[PacketSize]; // +1 cho Report ID
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 1) // ít nhất có ReportID + 1 byte data
                        {
                            // Bỏ qua byte đầu (report ID), chỉ lấy phần payload
                            byte[] received = new byte[bytesRead - 1];
                            Array.Copy(buffer, 1, received, 0, received.Length);

                            FrameReceived?.Invoke(received);
                            System.Diagnostics.Debug.WriteLine("✅ Raised FrameReceived event");
                        }
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }
        */
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
                            Console.WriteLine($"✅ FrameReceived invoked, CMD: {received[0]:X2}");
                        }
                        else
                        {
                            // Không đọc được gì, có thể thiết bị ngắt kết nối
                            System.Diagnostics.Debug.WriteLine("⚠️ No data received, possible disconnect.");
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
