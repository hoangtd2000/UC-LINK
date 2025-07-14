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
        private DispatcherTimer _comScanTimer;
        private string[] _lastPortNames = Array.Empty<string>();
        private bool _isComConnected = false;

        public MainWindow()
        {
            InitializeComponent();
            _lastPortNames = SerialPort.GetPortNames();
            SetupAutoComScan(); // gọi hàm khởi động quét COM tự động
            var cdcService = new CdcService();
            _cdcViewModel = new CdcViewModel(cdcService);
            DataContext = _cdcViewModel;
            _cdcViewModel.GetAppendCR = () => chkCR.IsChecked == true;
            _cdcViewModel.GetAppendLF = () => chkLF.IsChecked == true;

            cmbDisplayMode.SelectionChanged += (s, e) =>
            {
                var selected = ((ComboBoxItem)cmbDisplayMode.SelectedItem).Content.ToString();
                _cdcViewModel.SelectedDisplayMode = selected;
            };
            _cdcViewModel.AutoScrollRequest += () =>
            {
                txtReceiveCdcData.ScrollToEnd(); 
            };
            LoadComPorts();
            dgCdcSend.ItemsSource = _cdcViewModel.FramesToSend;

        }
        private void SetupAutoComScan()
        {
            _comScanTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _comScanTimer.Tick += (s, e) =>
            {
                if (_isComConnected) return;

                string[] currentPorts = SerialPort.GetPortNames();

                if (!_lastPortNames.SequenceEqual(currentPorts))
                {
                    _lastPortNames = currentPorts;
                    LoadComPorts();
                }
            };

            _comScanTimer.Start();
        }


        private void btnSendCdcData_Click(object sender, RoutedEventArgs e)
        {
            _cdcViewModel.AppendCR = chkCR.IsChecked == true;
            _cdcViewModel.AppendLF = chkLF.IsChecked == true;
        }

        private void btnSendCanFrame_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void BtnOpenCom_Click(object sender, RoutedEventArgs e)
        {
            _isComConnected = true;
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
            _isComConnected = false;
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



    }
}
