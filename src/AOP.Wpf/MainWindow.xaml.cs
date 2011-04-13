using System.Windows;
using AOP.Lib;

namespace AOP.Wpf
{
    /// <summary>
    ///   Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public StockTickerInpc StockTickerInpc { get; private set; }
        public StockTickerPoco StockTickerPoco { get; private set; }
        public StockTickerVerbose StockTickerVerbose { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            StockTickerInpc = new StockTickerInpc();
            StockTickerPoco = new StockTickerPoco();
            StockTickerVerbose = new StockTickerVerbose();
            DataContext = this;
        }
    }
}