using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using UsbComposite.Helpers;

namespace UsbComposite.Viewmodels
{
    public class CanConfigViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<int> BaudRateOptions { get; } = new ObservableCollection<int> { 125, 250, 500, 750, 1000 };

        private int _selectedBaudRate = 500;
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set { _selectedBaudRate = value; OnPropertyChanged(); }
        }

        private bool _isStandardIdFilter = true;
        public bool IsStandardIdFilter
        {
            get => _isStandardIdFilter;
            set
            {
                if (_isStandardIdFilter != value)
                {
                    _isStandardIdFilter = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsExtendedIdFilter));
                    SetFilterRangeByIdType(); // cập nhật range khi chọn Standard
                }
            }
        }

        public bool IsExtendedIdFilter
        {
            get => !_isStandardIdFilter;
            set
            {
                IsStandardIdFilter = !value; // phản chiếu lại
            }
        }

        private string _filterFromId;
        public string FilterFromId
        {
            get => _filterFromId;
            set { _filterFromId = value; OnPropertyChanged(); }
        }

        private string _filterToId;
        public string FilterToId
        {
            get => _filterToId;
            set { _filterToId = value; OnPropertyChanged(); }
        }


        private string _samplePointText = "87.5";
        public string SamplePointText
        {
            get => _samplePointText;
            set
            {
                if (_samplePointText != value)
                {
                    _samplePointText = value;
                    OnPropertyChanged();

                    // Thử chuyển đổi sang float
                    if (float.TryParse(_samplePointText, out float sp))
                    {
                        SamplePoint = sp;
                    }
                }
            }
        }

        private float _samplePoint = 87.5f;
        public float SamplePoint
        {
            get => _samplePoint;
            set
            {
                if (_samplePoint != value)
                {
                    _samplePoint = value;
                    OnPropertyChanged();

                    OnPropertyChanged(nameof(SyncSegWidth));
                    OnPropertyChanged(nameof(Seg1Width));
                    OnPropertyChanged(nameof(Seg2Width));
                }
            }
        }


        private double _totalWidth = 200.0;
        public double TotalWidth
        {
            get => _totalWidth;
            set
            {
                if (_totalWidth != value)
                {
                    _totalWidth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SyncSegWidth));
                    OnPropertyChanged(nameof(Seg1Width));
                    OnPropertyChanged(nameof(Seg2Width));
                }
            }
        }

        private const double TotalTQ = 20.0;

        public GridLength SyncSegWidth => new GridLength(1, GridUnitType.Star); // 1 TQ

        public GridLength Seg1Width
        {
            get
            {
                double sampleRatio = SamplePoint / 100.0;
                double seg1TQ = sampleRatio * TotalTQ - 1;
                return new GridLength(Math.Max(0, seg1TQ), GridUnitType.Star);
            }
        }

        public GridLength Seg2Width
        {
            get
            {
                double sampleRatio = SamplePoint / 100.0;
                double seg2TQ = TotalTQ - (sampleRatio * TotalTQ);
                return new GridLength(Math.Max(0, seg2TQ), GridUnitType.Star);
            }
        }

        // SamplePoint đã có từ trước: float SamplePoint (ví dụ: 0.875)



        public ICommand SetDefaultFilterValuesCommand { get; }

        public CanConfigViewModel()
        {
            SetDefaultFilterValuesCommand = new RelayCommand(SetDefaults);
            SetDefaults(); // gọi khi UI khởi tạo
        }

        private void SetDefaults()
        {
            // gọi setter để trigger OnPropertyChanged
            SelectedBaudRate = 500;

            // ⚠ Gọi riêng logic thay vì dùng IsStandardIdFilter để đảm bảo OnPropertyChanged hoạt động chính xác
            _isStandardIdFilter = true;
            OnPropertyChanged(nameof(IsStandardIdFilter));
            OnPropertyChanged(nameof(IsExtendedIdFilter));

            // ⚠ Gọi tay thay vì gọi gián tiếp từ IsStandardIdFilter
            FilterFromId = "0";
            FilterToId = "7FF";
            SamplePoint = 87.5f;
            SamplePointText = "87.5";

            Console.WriteLine("🚀 SetDefaults called:");
            Console.WriteLine($"SelectedBaudRate: {SelectedBaudRate}");
            Console.WriteLine($"IsStandardIdFilter: {IsStandardIdFilter}");
            Console.WriteLine($"FilterFromId: {FilterFromId}");
            Console.WriteLine($"FilterToId: {FilterToId}");
        }


        private void SetFilterRangeByIdType()
        {
            FilterFromId = "0";
            FilterToId = IsStandardIdFilter ? "7FF" : "1FFFFFFF";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
