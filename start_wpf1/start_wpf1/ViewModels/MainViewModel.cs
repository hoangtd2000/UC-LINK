using start_wpf1.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace start_wpf1.ViewModels
{
    public class MainViewModel 
    {
        public CdcViewModel CdcVM { get; }
        public CanViewModel CanVM { get; }

        public MainViewModel()
        {
            CdcVM = new CdcViewModel(new CdcService());
            CanVM = new CanViewModel();
        }
    }
}
