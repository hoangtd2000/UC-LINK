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
        private readonly MainViewModel _mainViewModel;


        private DispatcherTimer _comScanTimer;
        private string[] _lastPortNames = Array.Empty<string>();
        private bool _isComConnected = false;


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
            // CdcViewModel logic
            _mainViewModel.CdcVM.GetAppendCR = () => chkCR.IsChecked == true;
            _mainViewModel.CdcVM.GetAppendLF = () => chkLF.IsChecked == true;

            _lastPortNames = SerialPort.GetPortNames();
            SetupAutoComScan();

            cmbDisplayMode.SelectionChanged += (s, e) =>
            {
                var selected = ((ComboBoxItem)cmbDisplayMode.SelectedItem).Content.ToString();
                _mainViewModel.CdcVM.SelectedDisplayMode = selected;
            };

            _mainViewModel.CdcVM.AutoScrollRequest += () =>
            {
                //txtReceiveCdcData.ScrollToEnd();
                /*Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (lstReceiveLines.Items.Count > 0)
                    {
                        object lastItem = lstReceiveLines.Items[lstReceiveLines.Items.Count - 1];
                        lstReceiveLines.UpdateLayout(); // đảm bảo đã render
                        lstReceiveLines.ScrollIntoView(lastItem);
                    }
                }), DispatcherPriority.ContextIdle);

                */
            };
            


            LoadComPorts();

            // DataGrid Cdc
            dgCdcSend.ItemsSource = _mainViewModel.CdcVM.FramesToSend;
        }
        private void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CdcVM.AutoScrollRequest += () =>
            {
                if (lstReceiveLines.Items.Count > 0)
                {
                    lstReceiveLines.ScrollIntoView(lstReceiveLines.Items[lstReceiveLines.Items.Count - 1]);
                }
                
               /* Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (lstReceiveLines.Items.Count > 0)
                    {
                        object lastItem = lstReceiveLines.Items[lstReceiveLines.Items.Count - 1];
                        lstReceiveLines.UpdateLayout(); // đảm bảo đã render
                        lstReceiveLines.ScrollIntoView(lastItem);
                    }
                }), DispatcherPriority.ContextIdle);
                */
            };
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
            _mainViewModel.CdcVM.AppendCR = chkCR.IsChecked == true;
            _mainViewModel.CdcVM.AppendLF = chkLF.IsChecked == true;
        }
        private void btnSendFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Files|*.txt;*.hex;*.bin;*.dec|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _mainViewModel.CdcVM.SendFile(dialog.FileName);
                txtLastSentFile.Text = $"Đã gửi: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }
        private void btnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                FileName = $"UC-LINK_cdc_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                _mainViewModel.CdcVM.SaveLogToFile(dialog.FileName);
            }
        }

        private void btnClearCanReceive_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CanVM.ReceivedFrames.Clear();
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
                _mainViewModel.CdcVM.OpenSerial(port, baud, parity, databits, stopBits);
                txtConnectionStatus.Text = $"Opened {port}";
                txtDeviceName.Text = $"UC-Link";
                btnOpenCom.IsEnabled = false;
                btnCloseCom.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Open comport ERROR: {ex.Message}");
            }
        }

        private void btnCloseCom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _mainViewModel.CdcVM.CloseSerial();
                txtConnectionStatus.Text = "Disconect !";
                btnOpenCom.IsEnabled = true;
                btnCloseCom.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Close comport ERROR: {ex.Message}");
            }
            _isComConnected = false;
        }

        private void btnClearCdcReceive_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.CdcVM.ClearReceive(); // gọi đúng cách
        }



        private void TxtReceiveCdcData_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
        private void BtnConnectCan_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Đã bấm Kết nối CAN");
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

        private void btnSendCanFrame_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
