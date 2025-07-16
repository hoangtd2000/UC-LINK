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

            var packet = new byte[PacketSize];
            Array.Copy(data, packet, Math.Min(data.Length, PacketSize));

            try
            {
                _stream.Write(packet);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void StartListening(CancellationToken token)
        {
            Task.Run(() =>
            {
                var buffer = new byte[PacketSize];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = _stream.Read(buffer, 0, PacketSize);
                        if (bytesRead > 0)
                        {
                            byte[] received = new byte[bytesRead];
                            Array.Copy(buffer, received, bytesRead);
                            FrameReceived?.Invoke(received);
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

        public void Dispose()
        {
            Disconnect();
        }
    }
}
