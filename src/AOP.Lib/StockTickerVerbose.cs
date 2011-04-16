using System.ComponentModel;
using System.Windows;

namespace AOP.Lib
{
    public class StockTickerVerbose : DependencyObject, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _name;

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    var e = PropertyChanged;
                    if (e != null)
                        e(this, new PropertyChangedEventArgs("Name"));
                }
            }
        }

        private static readonly DependencyProperty PriceDependencyProperty =
            DependencyProperty.Register("Price", typeof(decimal), typeof(StockTickerVerbose));

        public decimal Price
        {
            get { return (decimal)GetValue(PriceDependencyProperty); }
            set { SetValue(PriceDependencyProperty, value); }
        }
    }
}