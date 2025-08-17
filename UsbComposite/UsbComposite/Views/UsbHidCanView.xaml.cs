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
    public partial class UsbHidCanView : UserControl
    {
        private readonly MainViewModel _mainViewModel;
        public UsbHidCanView(MainViewModel viewModel)
        {
            InitializeComponent();
            _mainViewModel = viewModel;
            DataContext = _mainViewModel;

            _mainViewModel.CanVM.ScrollToLatestFrame = () =>
            {
                if (dgCanReceive.Items.Count > 0)
                {
                    dgCanReceive.ScrollIntoView(dgCanReceive.Items[dgCanReceive.Items.Count - 1]);
                }
            };
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // Ngăn xuống dòng
                var current = sender as TextBox;
                if (current == null) return;

                current.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);
            }
        }


        private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb != null && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                tb.Focus();
            }
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text.Length >= tb.MaxLength)
            {
                tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

    }
}
