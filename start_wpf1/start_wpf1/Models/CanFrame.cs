using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace start_wpf1.Models
{
    public class CanFrame
    {
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss");
        public string CanId { get; set; } = "0x000";
        public int Dlc { get; set; } = 0;
        public string DataHex { get; set; } = "";
        public bool IsCyclic { get; set; } = false;
        public int CycleTimeMs { get; set; } = 1000;
        public bool IsEventTriggered { get; set; } = false;
    }
}
