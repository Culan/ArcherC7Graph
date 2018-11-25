using OxyPlot;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ArcherC7Graph
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ArcherC7 myC7;
        private BackgroundWorker backgroundWorker = new BackgroundWorker();
        private bool runBackgroundWorker = false;
        private OxyPlot.Series.LineSeries totalSeries = new OxyPlot.Series.LineSeries();

        private PlotModel _plotModel = new PlotModel();
        public PlotModel PlotModel
        {
            get
            {
                return _plotModel;
            }
            set
            {
                if (_plotModel != value)
                {
                    _plotModel = value;
                }
            }
        }

        private string _username = "admin";
        public string Username
        {
            get { return _username; }
            set
            {
                if (_username != value)
                {
                    _username = value;
                    if (myC7 != null)
                    {
                        myC7.Username = value;
                        RaisePropertyChanged("Username");
                    }
                }
            }
        }


        private string _password = "";
        public string Password
        {
            get { return _password; }
            set
            {
                if (_password != value)
                {
                    _password = value;
                    if (myC7 != null)
                    {
                        myC7.Password = value;
                        RaisePropertyChanged("Password");
                    }
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _plotModel.IsLegendVisible = true;
            _plotModel.LegendPlacement = LegendPlacement.Outside;
            _plotModel.LegendPosition = LegendPosition.BottomCenter;
            _plotModel.LegendOrientation = LegendOrientation.Horizontal;

            OxyPlot.Axes.LinearAxis LAY = new OxyPlot.Axes.LinearAxis()
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Megabit per second",
            };
            OxyPlot.Axes.DateTimeAxis LAX = new OxyPlot.Axes.DateTimeAxis()
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                MinorStep = 5,
                StringFormat = "HH:mm:ss"
            };

            _plotModel.Axes.Add(LAY);
            _plotModel.Axes.Add(LAX);

            totalSeries.Title = "TOTAL";
            _plotModel.Series.Add(totalSeries);

            plotView.Model = PlotModel;
            _plotModel.InvalidatePlot(true);

            myC7 = new ArcherC7();

            backgroundWorker.DoWork += backgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorker.WorkerReportsProgress = true;

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                runBackgroundWorker = false;
                myC7.Logout();
            }
            catch (Exception ex2)
            {
                MessageBox.Show("Error: " + ex2.Message);

            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void backgroundWorker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                myC7.getDhcp();

                while (runBackgroundWorker)
                {
                    List<KeyValuePair<string,string>> stats = myC7.getStats();
                    backgroundWorker.ReportProgress(1, stats);
                    Thread.Sleep(10000);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void backgroundWorker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            List<KeyValuePair<string, string>> stats = (List<KeyValuePair<string, string>>)e.UserState;
            double total = 0;

            foreach (KeyValuePair<string,string> valuePair in stats)
            {
                bool seriesFound = false;

                foreach (OxyPlot.Series.Series series in _plotModel.Series)
                {
                    // Series found
                    if (series.Title.Equals(valuePair.Key))
                    {
                        ((OxyPlot.Series.LineSeries)series).Points.Add(new DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(DateTime.Now), ((Double.Parse(valuePair.Value) / 1000000) * 8)));
                        Debug.Write(" " + ((Double.Parse(valuePair.Value) / 1000000) * 8));
                        total += ((Double.Parse(valuePair.Value) / 1000000) * 8);
                        seriesFound = true;
                    }
                }

                if (!seriesFound)
                {
                    // Series not found, add series
                    OxyPlot.Series.LineSeries lineSeries = new OxyPlot.Series.LineSeries();
                    lineSeries.Title = valuePair.Key;
                    _plotModel.Series.Add(lineSeries);
                    lineSeries.Points.Add(new DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(DateTime.Now), ((Double.Parse(valuePair.Value) / 1000000) * 8)));
                    total += ((Double.Parse(valuePair.Value) / 1000000) * 8);
                }

            }

            totalSeries.Points.Add(new DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(DateTime.Now), total));

            plotView.Model = PlotModel;
            _plotModel.InvalidatePlot(true);
        }

        private void AutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!myC7.LoggedIn)
            {
                for (int i = 0; i <= 3; i++)
                {
                    if (myC7.Login())
                    {
                        Password = "";
                        break;
                    }
                }

                if (myC7.LoggedIn)
                {
                    runBackgroundWorker = true;
                    backgroundWorker.RunWorkerAsync();
                }
                else
                {
                    MessageBox.Show("Failed 3 connection attempts");
                }

            }
        }

        private void StopAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            runBackgroundWorker = false;
        }

        private void UpdateDhcpButton_Click(object sender, RoutedEventArgs e)
        {
            if (myC7 != null)
            {
                myC7.getDhcp();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void RaisePropertyChanged(string propertyName)
        {
            var handlers = PropertyChanged;

            handlers(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Application.Current.MainWindow.Height > ButtonPanel.Height + 10)
            {
                plotView.Height = Application.Current.MainWindow.Height - ButtonPanel.Height - 10;
            }
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            plotView.ResetAllAxes();
        }
    }
}
