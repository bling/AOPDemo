using System.Windows;
using AOP.Lib;

namespace AOP.Wpf
{
    /// <summary>
    ///   Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public StockTicker StockTicker { get; private set; }
        public StockTickerVerbose StockTickerVerbose { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            StockTicker = new StockTicker();
            StockTickerVerbose = new StockTickerVerbose();
            DataContext = this;
        }
    }
}