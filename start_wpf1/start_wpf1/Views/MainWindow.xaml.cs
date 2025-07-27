using System.Windows;
using System.IO.Ports;
using System.Collections.ObjectModel;
using start_wpf1.Models;
using System;
using System.Windows.Threading;
using start_wpf1.ViewModels;
using start_wpf1.Service;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Media;
using System.Text;


namespace start_wpf1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _mainViewModel;



        private string[] _lastPortNames = Array.Empty<string>();



        public MainWindow()
        {
            InitializeComponent();

            // Tạo MainViewModel
            _mainViewModel = new MainViewModel();
            this.DataContext = _mainViewModel; // GÁN duy nhất 1 DataContext
                                               // Gắn auto scroll cho CAN
            _mainViewModel.CanVM.ScrollToLatestFrame = () =>
            {
                if (dgCanReceive.Items.Count > 0)
                {
                    dgCanReceive.ScrollIntoView(dgCanReceive.Items[dgCanReceive.Items.Count - 1]);
                }
            };


            cmbDisplayMode.SelectionChanged += (s, e) =>
            {
                var selected = ((ComboBoxItem)cmbDisplayMode.SelectedItem).Content.ToString();
                _mainViewModel.CdcVM.SelectedDisplayMode = selected;
            };
            
            
            _mainViewModel.CdcVM.AutoScrollRequest += () =>
            {
                txtReceiveLog.Dispatcher.InvokeAsync(() =>
                {
                    txtReceiveLog.ScrollToEnd();
                }, DispatcherPriority.Background);
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

        public static ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer) return (ScrollViewer)obj;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }



        private void TxtReceiveCdcData_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }

        private void LoadComPorts()
        {
            var availablePorts = SerialPort.GetPortNames()
                .Distinct()
                .Where(IsComPortAvailable)
                .OrderBy(p => p)
                .ToArray();

            var selected = cmbComPorts.SelectedItem as string;

            // So sánh danh sách hiện tại với danh sách đang hiển thị
            var comboBoxPorts = cmbComPorts.Items.Cast<string>().ToArray();
            if (availablePorts.SequenceEqual(comboBoxPorts))
                return;

            cmbComPorts.Items.Clear();

            foreach (var port in availablePorts)
            {
                cmbComPorts.Items.Add(port);
            }

            // 👉 Nếu người dùng đã chọn COM nào đó → giữ lại
            if (!string.IsNullOrEmpty(selected) && availablePorts.Contains(selected))
            {
                cmbComPorts.SelectedItem = selected;
            }
            else if (availablePorts.Length > 0)
            {
                // ✅ Tự động chọn COM đầu tiên nếu danh sách không rỗng
                cmbComPorts.SelectedIndex = 0;
            }
            else
            {
                cmbComPorts.SelectedIndex = -1; // Không có COM nào
            }

            Console.WriteLine("Current Ports (filtered): " + string.Join(", ", availablePorts));
        }

        private bool IsComPortAvailable(string portName)
        {
            try
            {
                using (var testPort = new SerialPort(portName))
                {
                    testPort.Open();
                    testPort.Close();
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Có thiết bị COM đang chiếm giữ port → vẫn là COM thật
                return true;
            }
            catch
            {
                // Lỗi khác (ví dụ COM ghost) → không dùng được
                return false;
            }
        }
        private void ScrollToLastItemInListBox(ListBox listBox)
        {
            if (listBox.Items.Count == 0) return;

            object lastItem = listBox.Items[listBox.Items.Count - 1];

            // Bắt LayoutUpdated (gọi sau khi render xong)
            EventHandler handler = null;
            handler = (s, e) =>
            {
                listBox.LayoutUpdated -= handler;

                var listBoxItem = listBox.ItemContainerGenerator.ContainerFromItem(lastItem) as FrameworkElement;
                listBoxItem?.BringIntoView(); // Ép scroll tới item
            };

            listBox.LayoutUpdated += handler;
        }
        private void btnSendCanFrame_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
