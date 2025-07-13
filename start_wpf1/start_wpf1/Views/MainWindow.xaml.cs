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
namespace start_wpf1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CdcViewModel _cdcViewModel;

        public MainWindow()
        {
            InitializeComponent();

            var cdcService = new CdcService();
            _cdcViewModel = new CdcViewModel(cdcService);
            DataContext = _cdcViewModel;
            cmbDisplayMode.SelectionChanged += (s, e) =>
            {
                var selected = ((ComboBoxItem)cmbDisplayMode.SelectedItem).Content.ToString();
                _cdcViewModel.SelectedDisplayMode = selected;
            };
            _cdcViewModel.AutoScrollRequest += () =>
            {
                txtReceiveCdcData.ScrollToEnd(); // ✔ đúng với TextBox
            };


            LoadComPorts();

            dgCdcSend.ItemsSource = _cdcViewModel.FramesToSend;
        }



        private void btnSendCdcData_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void btnSendCanFrame_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void BtnOpenCom_Click(object sender, RoutedEventArgs e)
        {
            string port = cmbComPorts.SelectedItem?.ToString() ?? "";
            int baud = int.Parse(((ComboBoxItem)cmbBaudRate.SelectedItem).Content.ToString());
            int databits = int.Parse(((ComboBoxItem)cmbDataBits.SelectedItem).Content.ToString());
            var parity = (Parity)Enum.Parse(typeof(Parity), ((ComboBoxItem)cmbParity.SelectedItem).Content.ToString());
            var stopBits = (StopBits)Enum.Parse(typeof(StopBits), ((ComboBoxItem)cmbStopBits.SelectedItem).Content.ToString());

            try
            {
                _cdcViewModel.OpenSerial(port, baud, parity, databits, stopBits);
                txtConnectionStatus.Text = $"opened {port}";
                btnOpenCom.IsEnabled = false;
                btnCloseCom.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở cổng: {ex.Message}");
            }
        }

        private void btnCloseCom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cdcViewModel.CloseSerial();
                txtConnectionStatus.Text = "Đã ngắt kết nối";
                btnOpenCom.IsEnabled = true;
                btnCloseCom.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đóng COM: {ex.Message}");
            }
        }

        private void btnClearCdcReceive_Click(object sender, RoutedEventArgs e)
        {
            _cdcViewModel.ClearReceive(); // gọi đúng cách
        }

        private void btnClearCanReceive_Click(object sender, RoutedEventArgs e)
        {
            // TODO: xử lý xóa dữ liệu CAN Receive, ví dụ như:
            dgCanReceive.ItemsSource = null;
        }

        private void TxtReceiveCdcData_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
        private void LoadComPorts()
        {
            var current = cmbComPorts.SelectedItem?.ToString();
            cmbComPorts.Items.Clear();

            var ports = SerialPort.GetPortNames();
            Array.Sort(ports); // sắp xếp COM1, COM2, ...

            foreach (var port in ports)
            {
                cmbComPorts.Items.Add(port);
            }

            if (ports.Contains(current))
            {
                cmbComPorts.SelectedItem = current;
            }
            else if (cmbComPorts.Items.Count > 0)
            {
                cmbComPorts.SelectedIndex = 0;
            }
        }
        


    }
}
