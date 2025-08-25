using Esri.ArcGISRuntime.UI.Controls;
using System.Windows;

namespace HouseWithoutCars
{
    public partial class MainWindow : Window
    {
        private MapViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MapViewModel();
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (MyMapView != null)
            {
                _viewModel.SetMapView(MyMapView, AssociationLegend);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Warning: MapView control not found!");
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 清理资源
            _viewModel?.Cleanup();
        }
    }
}