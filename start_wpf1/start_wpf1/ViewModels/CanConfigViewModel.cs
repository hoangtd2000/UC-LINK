using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using start_wpf1.Helpers;

namespace start_wpf1.ViewModels
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

        public ICommand SetDefaultFilterValuesCommand { get; }

        public CanConfigViewModel()
        {
            SetDefaultFilterValuesCommand = new RelayCommand(SetDefaults);
            Console.WriteLine("🔥 ViewModel Constructor gọi trước cả khi Tab được nhìn thấy");
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
