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
using System.Windows.Shapes;
using UsbComposite.Viewmodels;

namespace UsbComposite.Views
{
    /// <summary>
    /// Interaction logic for ConnectDeviceView.xaml
    /// </summary>
    public partial class ConnectDeviceView : UserControl
    {
        private MainViewModel _mainViewModel;
        public ConnectDeviceView()
        {
            InitializeComponent();
            _mainViewModel = (MainViewModel)((MainWindow)Application.Current.MainWindow).DataContext;
        }

        private void CmbComPorts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void BitTimingBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_mainViewModel?.CanVM.Config != null)
            {
                _mainViewModel.CanVM.Config.TotalWidth = e.NewSize.Width;
            }
        }
        private void SamplePointTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel?.CanVM?.Config != null)
            {
                string input = _mainViewModel.CanVM.Config.SamplePointText;
                float sp;

                if (float.TryParse(input, out sp))
                {
                    // Giới hạn giá trị
                    if (sp < 50f) sp = 50f; 
                    else if (sp > 90f)sp = 90f; 
                    

                        _mainViewModel.CanVM.Config.SamplePoint = sp; // nếu bạn dùng thuộc tính float SamplePoint
                    _mainViewModel.CanVM.Config.SamplePointText = sp.ToString("F1");
                }
                else
                {
                    // Báo lỗi nếu nhập sai định dạng
                    // Reset về giá trị mặc định
                    _mainViewModel.CanVM.Config.SamplePoint = 87.5f;
                    _mainViewModel.CanVM.Config.SamplePointText = "87.5";
                }
            }
        }
    }
}
