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
using System.Windows.Threading;
using UsbComposite.Viewmodels;

namespace UsbComposite.Views
{
    /// <summary>
    /// Interaction logic for UsbComportView.xaml
    /// </summary>
   
    public partial class UsbComportView : UserControl
    {



        private readonly MainViewModel _mainViewModel;
        public UsbComportView(MainViewModel viewModel)
        {
            InitializeComponent();
            _mainViewModel = viewModel;
            DataContext = _mainViewModel;
            
            cmbDisplayMode.SelectionChanged += (s, e) =>
            {
                var selected = ((ComboBoxItem)cmbDisplayMode.SelectedItem).Content.ToString();
                _mainViewModel.CdcVM.SelectedDisplayMode = selected;
            };
            



            _mainViewModel.CdcVM.NewLinesReceived += (textToAppend) =>
            {
                txtReceiveLog.Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrEmpty(textToAppend))
                    {
                        txtReceiveLog.Clear(); // Xóa toàn bộ khi nhận chuỗi rỗng
                    }
                    else
                    {
                        txtReceiveLog.AppendText(textToAppend);
                        txtReceiveLog.ScrollToEnd();
                    }
                });
            };
            dgCdcSend.ItemsSource = _mainViewModel.CdcVM.FramesToSend;

        }


    }
}
