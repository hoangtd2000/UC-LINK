using System.Windows;
using start_wpf1.ViewModels;

namespace start_wpf1
{
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
