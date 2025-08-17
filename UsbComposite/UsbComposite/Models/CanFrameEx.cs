using System;
using System.Collections.ObjectModel;
using System.Linq;
using UsbComposite.Models;

public class CanFrameEx : CanFrame
{

    public CanFrameEx()
    {
        this.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CycleTimeMsDisplay))
            {
                System.Diagnostics.Debug.WriteLine("CycleTimeMsDisplay property changed event fired.");
            }
        };
    }

    private double _cycleTimeMsInt = 1000.0;
    public double CycleTimeMsInt
    {
        get => _cycleTimeMsInt;
        set
        {
            _cycleTimeMsInt = value;
            System.Diagnostics.Debug.WriteLine($"CycleTimeMsInt updated: {_cycleTimeMsInt:F1} ms");
            OnPropertyChanged(nameof(CycleTimeMsInt));
            OnPropertyChanged(nameof(CycleTimeMsDisplay));
        }
    }
    private int _count = 1; // mặc định là 1 khi frame mới xuất hiện
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                OnPropertyChanged(nameof(Count));
            }
        }
    }

    //public string CycleTimeMsDisplay => $"{CycleTimeMsInt} ms";
    public string CycleTimeMsDisplay => $"{CycleTimeMsInt / 1.0:F1}";
    public uint LastTimestampFromMcu { get; set; } = 0;

    /// <summary>
    /// Cập nhật giá trị chu kỳ dựa trên timestamp mới
    /// </summary>
    public void UpdateCycleTime(uint newTimestampFromMcu)
    {
        System.Diagnostics.Debug.WriteLine($"[UpdateCycleTime] Last: {LastTimestampFromMcu}, New: {newTimestampFromMcu}");

        long diff = (long)newTimestampFromMcu - (long)LastTimestampFromMcu;
        if (diff < 0)
        {
            diff += 0x100000000; // handle overflow
            System.Diagnostics.Debug.WriteLine($"[UpdateCycleTime] Overflow handled, diff = {diff}");
        }

        int calculatedCycle = (int)(diff / 1000); // giả sử timestamp đơn vị là us → ms

        if (calculatedCycle <= 0 || calculatedCycle > 100000)
        {
            calculatedCycle = 1000; // default fallback
            System.Diagnostics.Debug.WriteLine($"[UpdateCycleTime] Invalid or out-of-range cycle. Reset to default 1000");
        }

        System.Diagnostics.Debug.WriteLine($"[UpdateCycleTime] Final CycleTimeMsInt = {calculatedCycle}");

        CycleTimeMsInt = calculatedCycle;
    }

    /*
    public uint CanIdAsUInt
    {
        get
        {
            if (string.IsNullOrEmpty(CanId))
                return 0;
            // Loại bỏ "0x", parse hex
            var s = CanId.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? CanId.Substring(2) : CanId;
            if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint val))
                return val;
            return 0;
        }
    }
    */
    public uint CanIdAsUInt
    {
        get
        {
            if (string.IsNullOrEmpty(CanId))
                return 0;

            // Bỏ "0x", parse hex
            var s = CanId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? CanId.Substring(2)
                        : CanId;

            if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint val))
            {
                if (FrameType == CanFrameType.Standard)
                    return val & 0x7FF;        // 11-bit
                else
                    return val & 0x1FFFFFFF;   // 29-bit
            }

            return 0;
        }
    }





    /// <summary>
    /// Cập nhật dữ liệu DataBytesHex và Dlc, đăng ký event cho từng byte để update DataHex
    /// </summary>
    public void UpdateData(byte[] newData, byte newDlc)
    {
        Dlc = newDlc;

        DataBytesHex = new ObservableCollection<BindableByte>(
            newData.Select(b => new BindableByte { Value = b.ToString("X2") }));

        foreach (var b in DataBytesHex)
        {
            b.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BindableByte.Value))
                {
                    OnPropertyChanged(nameof(DataHex));
                }
            };
        }

        OnPropertyChanged(nameof(DataBytesHex));
        OnPropertyChanged(nameof(Dlc));
        OnPropertyChanged(nameof(DataHex));
    }
}
