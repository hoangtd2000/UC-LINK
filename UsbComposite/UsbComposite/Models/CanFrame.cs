using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UsbComposite.Models
{
    public class CanFrame : INotifyPropertyChanged
    {


        public enum CanFrameType
        {
            Standard,
            Extended
        }

        public ObservableCollection<BindableByte> DataBytesHex { get; set; } = new ObservableCollection<BindableByte>(Enumerable.Range(0, 8).Select(i => new BindableByte()).ToList());
        public IEnumerable<BindableByte> VisibleDataBytes => DataBytesHex.Take(Dlc);
        private bool _isCyclic;

        private string _canId = "000";
        public string CanId
        {
            get => _canId;
            set
            {
                string input = (value ?? "").Trim().ToUpper();

                // Bỏ "0x" nếu có
                if (input.StartsWith("0X"))
                    input = input.Substring(2);

                // Mặc định fallback
                string fallback = FrameType == CanFrameType.Standard ? "7FF" : "1FFFFFFF";

                // Thử parse
                if (!uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out uint id))
                {
                    _canId = fallback;
                }
                else
                {
                    if (FrameType == CanFrameType.Standard && id > 0x7FF)
                        _canId = "7FF";
                    else if (FrameType == CanFrameType.Extended && id > 0x1FFFFFFF)
                        _canId = "1FFFFFFF";
                    else
                        _canId = id.ToString("X"); // Ghi lại theo chuẩn Hex (viết hoa)
                }

                OnPropertyChanged(nameof(CanId));
            }
        }

        private byte _dlc = 8;

        public byte Dlc
        {
            get => _dlc;
            set
            {
                if (_dlc != value)
                {
                    _dlc = value;
                    OnPropertyChanged(nameof(Dlc));
                    OnPropertyChanged(nameof(VisibleDataBytes));
                }
            }
        }

        private CanFrameType _frameType = CanFrameType.Standard;
        public CanFrameType FrameType
        {
            get => _frameType;
            set
            {
                if (_frameType != value)
                {
                    _frameType = value;
                    OnPropertyChanged(nameof(FrameType));
                }
            }
        }

        private static int _globalIndexCounter = 0;

        private int _frameIndex;
        public int FrameIndex => _frameIndex; // read-only

        public CanFrame()
        {
            _frameIndex = Interlocked.Increment(ref _globalIndexCounter);
        }



        public bool IsCyclic
        {
            get { return _isCyclic; }
            set
            {
                if (_isCyclic != value)
                {
                    _isCyclic = value;
                    OnPropertyChanged(nameof(IsCyclic));
                }
            }
        }

        private string _cycleTimeMsReceived = "0";
        public string CycleTimeMsReceived
        {
            get => _cycleTimeMsReceived;
            set
            {
                _cycleTimeMsReceived = value;
                OnPropertyChanged(nameof(CycleTimeMsReceived));
            }
        }


        // public int CycleTimeMs { get; set; } = 1000;
        private string _cycleTimeMs = "1000";
        public string CycleTimeMs
        {
            get => _cycleTimeMs;
            set
            {
                var input = (value ?? "").Trim();

                if (int.TryParse(input, out int result))
                {
                    if (result >= 5 && result <= 500000)
                        _cycleTimeMs = result.ToString();
                    else
                        _cycleTimeMs = "1000"; // ngoài giới hạn
                }
                else
                {
                    _cycleTimeMs = "1000"; // sai kiểu
                }

                OnPropertyChanged(nameof(CycleTimeMs));
            }
        }


        public DateTime Timestamp { get; set; }

        public string DataHex => string.Join(" ", DataBytesHex.Take(Dlc).Select(b => b.Value));



        /*******************************************************************************/
        /* Custom CAN Frame Structure (for USB-CAN transmission)                       */
        /*-----------------------------------------------------------------------------*/
        /* Offset | Field        | Size | Value        | Description                   */
        /*--------|--------------|------|--------------|-------------------------------*/
        /* 0      | CMD          |  1   | 0x02         | Command: Send CAN frame       */
        /* 1      | CAN_ID[3]    |  1   | MSB          | CAN ID (32-bit, Big Endian)   */
        /* 2      | CAN_ID[2]    |  1   |              |                               */
        /* 3      | CAN_ID[1]    |  1   |              |                               */
        /* 4      | CAN_ID[0]    |  1   | LSB          |                               */
        /* 5      | DLC+Type     |  1   |              | Bit 7..4 = DLC (max 8 bytes)  */
        /*        |              |      |              | Bit 3 = Frame Type (0 = Std,  */
        /*        |              |      |              |                     1 = Ext)   */
        /*        |              |      |              | Bit 2..0 = Reserved (0)       */
        /* 6      | Data[0]      |  1   |              | Data byte 0                   */
        /* 7      | Data[1]      |  1   |              | Data byte 1                   */
        /* 8      | Data[2]      |  1   |              | ... up to DLC count           */
        /* ...    | ...          | ...  |              |                               */
        /* 13     | Data[7]      |  1   |              | Max 8 bytes (DLC ≤ 8)         */
        /*******************************************************************************/

        public byte[] ToBytes()
        {
            var buffer = new byte[64];
            buffer[0] = 0x02; // CMD gửi CAN

            // Chuẩn hóa và chuyển đổi CAN ID (tối đa 32-bit)
            string hexId = CanId;
            if (hexId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hexId = hexId.Substring(2);

            uint id = uint.Parse(hexId, System.Globalization.NumberStyles.HexNumber);

            // Ghi ID vào 4 byte (big-endian): buffer[1..4]
            buffer[1] = (byte)((id >> 24) & 0xFF);
            buffer[2] = (byte)((id >> 16) & 0xFF);
            buffer[3] = (byte)((id >> 8) & 0xFF);
            buffer[4] = (byte)(id & 0xFF);
            // DLC max = 8 (4 bit cao), loại Frame (4 bit thấp)
            byte frameTypeBit = (byte)(FrameType == CanFrameType.Extended ? 0x04 : 0x00);
            buffer[5] = (byte)((Dlc << 4) | frameTypeBit);

            // Ghi data từ buffer[6]
            for (int i = 0; i < Dlc && i < DataBytesHex.Count; i++)
            {
                var value = DataBytesHex[i].Value?.Trim();

                // Nếu trống hoặc sai format thì mặc định = 0
                if (string.IsNullOrWhiteSpace(value) || value.Length > 2)
                    buffer[6 + i] = 0x00;
                else
                    buffer[6 + i] = Convert.ToByte(value, 16);
            }
            return buffer;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
