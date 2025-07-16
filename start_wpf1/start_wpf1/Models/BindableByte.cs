using System.ComponentModel;

namespace start_wpf1.Models
{
    public class BindableByte : INotifyPropertyChanged
    {
        private string _value = "00";

        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public override string ToString() => Value;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
