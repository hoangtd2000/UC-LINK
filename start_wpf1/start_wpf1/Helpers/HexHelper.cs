using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace start_wpf1.Helpers
{
    public static class HexHelper
    {
        public static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();

            var bytes = new List<byte>();
            string[] parts = hex.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    bytes.Add(b);
                else
                    bytes.Add(0); // hoặc ném exception nếu bạn muốn nghiêm ngặt hơn
            }

            return bytes.ToArray();
        }
        
    }
}
