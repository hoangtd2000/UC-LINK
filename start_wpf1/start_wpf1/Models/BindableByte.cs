using System.ComponentModel;
using System.Text.RegularExpressions;

namespace start_wpf1.Models
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
                var trimmed = (value ?? "").Trim().ToUpper();

                // Kiểm tra nếu là hex hợp lệ
                if (!_hexRegex.IsMatch(trimmed))
                {
                    // Nếu không hợp lệ → giữ nguyên
                    return;
                }

                if (_value != trimmed)
                {
                    _value = trimmed;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public override string ToString() => Value;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
