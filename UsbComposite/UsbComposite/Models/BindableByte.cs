using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UsbComposite.Models
{
    public class BindableByte : INotifyPropertyChanged
    {
        private string _value = "00";
        private static readonly Regex _hexRegex = new Regex("^[0-9A-Fa-f]{1,2}$");

        public string Value
        {
            get => _value;
            set
            {
                string input = (value ?? "").Trim().ToUpper();

                // ✅ Nếu chuỗi trống → chấp nhận (cho phép xóa sạch)
                if (string.IsNullOrEmpty(input))
                {
                    _value = "";
                    OnPropertyChanged(nameof(Value));
                    return;
                }

                // ✅ Nếu hợp lệ hex thì mới cập nhật
                if (_hexRegex.IsMatch(input))
                {
                    _value = input;
                    OnPropertyChanged(nameof(Value));
                }
                // ❌ Nếu không hợp lệ → bỏ qua, không đổi (nhưng không cản xóa)
            }
        }
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString() => Value;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
