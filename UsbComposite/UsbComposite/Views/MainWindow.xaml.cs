using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UsbComposite.Viewmodels;
using UsbComposite.Views;
using UsbComposite.Service;
using UsbComposite.Models;

namespace UsbComposite.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public readonly MainViewModel _mainViewModel;
        private string[] _lastPortNames = Array.Empty<string>();

        // 🔹 Tạo các View private để tái sử dụng
        private ConnectDeviceView _connectDeviceView;
        private UsbHidCanView _usbHidCanView;
        private UsbComportView _usbComportView;

        public MainWindow()
        {
            InitializeComponent();

            // Tạo MainViewModel
            _mainViewModel = new MainViewModel();
            this.DataContext = _mainViewModel;

            // 🔹 Khởi tạo các View 1 lần duy nhất
            _connectDeviceView = new ConnectDeviceView();
            _usbHidCanView = new UsbHidCanView(_mainViewModel);
            _usbComportView = new UsbComportView(_mainViewModel);

            // Mặc định hiển thị tab đầu tiên nếu cần
            MainContentArea.Content = _connectDeviceView;
        }

        private void SidebarTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SidebarTabControl.SelectedIndex == -1)
                return;

            switch (SidebarTabControl.SelectedIndex)
            {
                case 0:
                    MainContentArea.Content = _connectDeviceView;
                    break;
                case 1:
                    MainContentArea.Content = _usbHidCanView;
                    break;
                case 2:
                    MainContentArea.Content = _usbComportView;
                    break;
                case 3:
                    MainContentArea.Content = new TextBlock { Text = "Thông tin thiết bị", FontSize = 16 };
                    break;
                case 4:
                    MainContentArea.Content = new TextBlock { Text = "Help", FontSize = 16 };
                    break;
            }
        }
    }
}
