using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;


namespace start_wpf1.Models
{
   public class CanFrame : INotifyPropertyChanged
{


    public ObservableCollection<BindableByte> DataBytesHex { get; set; }
        = new ObservableCollection<BindableByte>(Enumerable.Range(0, 8).Select(i => new BindableByte()).ToList());

    public IEnumerable<BindableByte> VisibleDataBytes => DataBytesHex.Take(Dlc);

    public string CanId { get; set; } = "000";

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

        public bool IsCyclic { get; set; }
    public int CycleTimeMs { get; set; } = 1000;
    public bool IsEventTriggered { get; set; }
    public DateTime Timestamp { get; set; }

    public string DataHex => string.Join(" ", DataBytesHex.Take(Dlc).Select(b => b.Value));

        public byte[] ToBytes()
        {
            var buffer = new byte[64];
            buffer[0] = (byte)0x02; // CMD gửi CAN

            // Không sửa trực tiếp CanId
            string hexId = CanId;
            if (hexId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hexId = hexId.Substring(2);

            int id = int.Parse(hexId, System.Globalization.NumberStyles.HexNumber);
            buffer[1] = (byte)((id >> 16) & 0xFF);
            buffer[2] = (byte)((id >> 8) & 0xFF);
            buffer[3] = (byte)(id & 0xFF);
            buffer[4] = (byte)Dlc;

            for (int i = 0; i < Dlc && i < DataBytesHex.Count; i++)
            {
                var value = DataBytesHex[i].Value?.Trim();

                // Nếu trống hoặc sai format thì mặc định = 0
                if (string.IsNullOrWhiteSpace(value) || value.Length > 2)
                    buffer[5 + i] = 0x00;
                else
                    buffer[5 + i] = Convert.ToByte(value, 16);
            }

            return buffer;
        }


        public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

}
