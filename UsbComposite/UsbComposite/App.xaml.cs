using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UsbComposite.Views;
using UsbComposite.Viewmodels;
namespace UsbComposite
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Tạo View
            // var mainWindow = new MainWindow();
            var mainWindow = new MainWindow();

            // Gán ViewModel
            //mainWindow.DataContext = new MainViewModel();

            // Hiển thị
            mainWindow.Show();
        }
    }
}
