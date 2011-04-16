using System.ComponentModel;

namespace AOP.Lib
{
    public class StockTickerInpc : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; set; }

        public decimal Price { get; set; }
    }

    public class StockTickerPoco
    {
        public string Name { get; set; }

        public decimal Price { get; set; }
    }
}