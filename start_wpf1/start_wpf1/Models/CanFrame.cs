using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;


namespace start_wpf1.Models
{
   public class CanFrame : INotifyPropertyChanged
{
    private int _dlc = 8;

    public ObservableCollection<BindableByte> DataBytesHex { get; set; }
        = new ObservableCollection<BindableByte>(Enumerable.Range(0, 8).Select(i => new BindableByte()).ToList());

    public IEnumerable<BindableByte> VisibleDataBytes => DataBytesHex.Take(Dlc);

    public string CanId { get; set; } = "0x123";

    public int Dlc
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
        buffer[0] = 0x01;

        if (CanId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            CanId = CanId.Substring(2);

        int id = int.Parse(CanId, System.Globalization.NumberStyles.HexNumber);
        buffer[1] = (byte)((id >> 16) & 0xFF);
        buffer[2] = (byte)((id >> 8) & 0xFF);
        buffer[3] = (byte)(id & 0xFF);
        buffer[4] = (byte)Dlc;

        for (int i = 0; i < Dlc && i < DataBytesHex.Count; i++)
        {
            buffer[5 + i] = Convert.ToByte(DataBytesHex[i].Value, 16);
        }

        return buffer;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

}
