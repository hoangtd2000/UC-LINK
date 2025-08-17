using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbComposite.Helpers
{
    public static class DataConverter
    {
        public static byte[] ConvertToBytes(string data, string type)
        {
            if (string.IsNullOrWhiteSpace(data))
                return Array.Empty<byte>();

            switch (type?.ToLowerInvariant())
            {
                case "hex":
                    return HexHelper.HexStringToBytes(data);
                case "dec":
                    return data.Split(' ', ',', ';')
                               .Select(s => byte.TryParse(s.Trim(), out var b) ? b : (byte)0)
                               .ToArray();
                case "ascii":
                default:
                    return Encoding.ASCII.GetBytes(data);
            }
        }
    }
}
