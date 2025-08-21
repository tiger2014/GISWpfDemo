using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using HouseWithoutCars.Componets;
using HouseWithoutCars.Model;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HouseWithoutCars
{
    public class MapViewModel : INotifyPropertyChanged
    {
        private Map _map;
        private GraphicsOverlayCollection _graphicsOverlays;
        private GraphicsOverlay _annotationsOverlay;
        private List<AnnotationModel> _annotations;
        private bool _isAddingMode;
        private readonly string _dataFilePath = "map_annotations.json";
        private MapView _mapView; // Need a MapView reference to handle events

        public MapViewModel()
        {
            InitializeMap();
            InitializeCommands();
            InitializeAnnotations();
        }

        // Set the MapView reference (called in MainWindow)
        public void SetMapView(MapView mapView)
        {
            _mapView = mapView;
            _mapView.Map = _map;
            _mapView.GraphicsOverlays = _graphicsOverlays;
            SetupMapViewEvents();
        }

        private void InitializeMap()
        {
            _map = new Map(SpatialReferences.WebMercator)
            {
                Basemap = new Basemap(BasemapStyle.ArcGISStreets)
            };

            Envelope texazExtent = new Envelope(-106.65, 25.84, -93.51, 36.5, SpatialReferences.Wgs84);
            _map.InitialViewpoint = new Viewpoint(texazExtent);

            // add TX layer
            var shapefileFeatureTable = new ShapefileFeatureTable("C:\\Develope\\GIS\\HouseWithoutCars\\Data\\State_Agency_Lands_3232346673015478260\\State_Agency_Lands.shp");
            var featureLayer = new FeatureLayer(shapefileFeatureTable);
            _map.OperationalLayers.Add(featureLayer);

            // Initialize GraphicsOverlays
            _graphicsOverlays = new GraphicsOverlayCollection();

            // Create the original point
            var point = new MapPoint(-95.9025, 29.5, SpatialReferences.Wgs84);
            var pointSymbol = new SimpleMarkerSymbol
            {
                Style = SimpleMarkerSymbolStyle.Circle,
                Color = System.Drawing.Color.Red,
                Size = 10
            };
            var pointGraphic = new Graphic(point, pointSymbol);
            var originalOverlay = new GraphicsOverlay();
            originalOverlay.Graphics.Add(pointGraphic);
            _graphicsOverlays.Add(originalOverlay);

            // Create a dedicated layer for annotations
            _annotationsOverlay = new GraphicsOverlay { Id = "Annotations" };
            _graphicsOverlays.Add(_annotationsOverlay);

            // Handle map load status
            _map.LoadStatusChanged += (s, e) =>
            {
                if (e.Status == Esri.ArcGISRuntime.LoadStatus.FailedToLoad)
                {
                    Debug.WriteLine($"Map failed to load, error: {_map.LoadError.Message}");
                    System.Windows.MessageBox.Show($"Map failed to load, error: {_map.LoadError.Message}");
                }
            };
        }

        private void InitializeCommands()
        {
            ClearAllCommand = new RelayCommand(async () => await ClearAllAnnotations());
            ExportCommand = new RelayCommand(ExportData);
        }

        private void InitializeAnnotations()
        {
            _annotations = new List<AnnotationModel>();
            LoadAnnotationsAsync();
        }

        private void SetupMapViewEvents()
        {
            if (_mapView != null)
            {
                _mapView.MouseMove += OnMapViewMouseMove;
                _mapView.MouseLeave += OnMapViewMouseLeave;
                _mapView.MouseRightButtonUp += OnMapViewRightClick;
            }
        }
        private async void OnMapViewRightClick(object sender, MouseButtonEventArgs e)
        {
            var screenPoint = e.GetPosition(_mapView);

            // Convert the screen point to map coordinates
            var mapPoint = _mapView.ScreenToLocation(screenPoint);

            if (mapPoint != null)
            {
                await AddAnnotationAtLocation(mapPoint);
            }
        }


        #region Properties

        public Map Map
        {
            get => _map;
            set { _map = value; OnPropertyChanged(); }
        }

        public GraphicsOverlayCollection GraphicsOverlays
        {
            get => _graphicsOverlays;
            set { _graphicsOverlays = value; OnPropertyChanged(); }
        }        

        #endregion

        #region Commands

        public ICommand ClearAllCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }

        #endregion

        #region Event Handlers

        private ToolTip _currentTooltip;
        

        private async void OnMapViewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                var identifyResults = await _mapView.IdentifyGraphicsOverlayAsync(
                    _annotationsOverlay, e.GetPosition(_mapView), 10, false);

                if (identifyResults.Graphics.Count > 0)
                {
                    var graphic = identifyResults.Graphics.First();
                    ShowTooltip(graphic, e.GetPosition(_mapView));
                }
                else
                {
                    HideTooltip();
                }
            }
            catch
            {
                // 
            }
        }

        private void OnMapViewMouseLeave(object sender, MouseEventArgs e)
        {
            HideTooltip();
        }

        #endregion

        #region Annotation Management

        private async Task AddAnnotationAtLocation(MapPoint location)
        {
            try
            {
                // Project to WGS84
                var wgs84Location = (MapPoint)GeometryEngine.Project(location, SpatialReferences.Wgs84);
                Debug.WriteLine($"AddAnnotationAtLocation--> location: X {wgs84Location.X}, Y {wgs84Location.Y}");
                var dialog = new AnnotationDialog();
                if (dialog.ShowDialog() == true)
                {
                    var annotation = new AnnotationModel
                    {
                        X = wgs84Location.X,  // <-- Ensure valid longitude and latitude are saved
                        Y = wgs84Location.Y,
                        Title = dialog.AnnotationTitle,
                        Description = dialog.AnnotationDescription,
                        Category = dialog.AnnotationCategory
                    };

                    _annotations.Add(annotation);
                    AddGraphicToMap(annotation);
                    await SaveAnnotationsAsync();

                    MessageBox.Show($"Annotation '{annotation.Title}' has been added!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add annotation: {ex.Message}");
            }
        }

        private void AddGraphicToMap(AnnotationModel annotation)
        {
            try
            {
                // Create a point based on the map's spatial reference
                var wgs84Point = new MapPoint(annotation.X, annotation.Y, SpatialReferences.Wgs84);

                var color = GetColorByCategory(annotation.Category);

                var symbol = new SimpleMarkerSymbol
                {
                    Style = SimpleMarkerSymbolStyle.Diamond,  // Changed to diamond, easier to see
                    Color = color,
                    Size = 20,  // Increased size, easier to see
                    Outline = new SimpleLineSymbol
                    {
                        Style = SimpleLineSymbolStyle.Solid,
                        Color = System.Drawing.Color.White,
                        Width = 3
                    }
                };

                var attributes = new Dictionary<string, object>
                {
                    ["Id"] = annotation.Id,
                    ["Title"] = annotation.Title,
                    ["Description"] = annotation.Description,
                    ["Category"] = annotation.Category,
                    ["CreatedTime"] = annotation.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["X"] = annotation.X.ToString("F6"),
                    ["Y"] = annotation.Y.ToString("F6")
                };

                var graphic = new Graphic(wgs84Point, attributes, symbol);
                _annotationsOverlay.Graphics.Add(graphic);

                // Debug info: output coordinates and number of graphics
                Debug.WriteLine($"AddGraphicToMap Added graphic at: {wgs84Point.X}, {wgs84Point.Y}");
                Debug.WriteLine($"AddGraphicToMap Total graphics in overlay: {_annotationsOverlay.Graphics.Count}");
                Debug.WriteLine($"AddGraphicToMap Map extent: {_mapView?.GetCurrentViewpoint(ViewpointType.BoundingGeometry)?.TargetGeometry}");

                //RefreshGraphics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding graphic: {ex.Message}");
                MessageBox.Show($"Failed to add graphic: {ex.Message}");
            }

        }
        private System.Drawing.Color GetColorByCategory(string category)
        {
            return category switch
            {
                "Most Important" => System.Drawing.Color.Red,
                "Important" => System.Drawing.Color.Orange,
                "Medium" => System.Drawing.Color.Blue,
                "Info" => System.Drawing.Color.Green,
                _ => System.Drawing.Color.Purple
            };
        }

        private async Task CheckForExistingAnnotation(Point screenPoint, MapPoint mapPoint)
        {
            try
            {
                var identifyResults = await _mapView.IdentifyGraphicsOverlayAsync(
                    _annotationsOverlay, screenPoint, 10, false);

                if (identifyResults.Graphics.Count > 0)
                {
                    var graphic = identifyResults.Graphics.First();
                    var annotationId = graphic.Attributes["Id"].ToString();
                    ShowAnnotationContextMenu(annotationId, screenPoint);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to check for annotation: {ex.Message}");
            }
        }

        private void ShowAnnotationContextMenu(string annotationId, Point screenPoint)
        {
            var contextMenu = new ContextMenu();

            var editItem = new MenuItem { Header = "Edit" };
            editItem.Click += (s, e) => EditAnnotation(annotationId);

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += (s, e) => DeleteAnnotation(annotationId);

            contextMenu.Items.Add(editItem);
            contextMenu.Items.Add(deleteItem);

            contextMenu.IsOpen = true;
            contextMenu.PlacementTarget = _mapView;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            contextMenu.HorizontalOffset = screenPoint.X;
            contextMenu.VerticalOffset = screenPoint.Y;
        }

        private async void EditAnnotation(string annotationId)
        {
            var annotation = _annotations.FirstOrDefault(a => a.Id == annotationId);
            if (annotation != null)
            {
                var dialog = new AnnotationDialog(annotation);
                if (dialog.ShowDialog() == true)
                {
                    annotation.Title = dialog.AnnotationTitle;
                    annotation.Description = dialog.AnnotationDescription;
                    annotation.Category = dialog.AnnotationCategory;

                    RefreshGraphics();
                    await SaveAnnotationsAsync();

                    MessageBox.Show("Annotation updated!");
                }
            }
        }

        private async void DeleteAnnotation(string annotationId)
        {
            if (MessageBox.Show("Are you sure you want to delete this annotation?", "Confirm Deletion",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _annotations.RemoveAll(a => a.Id == annotationId);
                RefreshGraphics();
                await SaveAnnotationsAsync();

                MessageBox.Show("Annotation deleted!");
            }
        }

        private void RefreshGraphics()
        {
            _annotationsOverlay.Graphics.Clear();
            foreach (var annotation in _annotations)
            {
                AddGraphicToMap(annotation);
            }
        }

        private async Task ClearAllAnnotations()
        {
            if (MessageBox.Show("Are you sure you want to clear all annotations?", "Confirm Clear",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _annotations.Clear();
                _annotationsOverlay.Graphics.Clear();
                await SaveAnnotationsAsync();
                MessageBox.Show("All annotations have been cleared!");
            }
        }

        #endregion

        #region Tooltip Management

        private void ShowTooltip(Graphic graphic, Point screenPoint)
        {
            if (_currentTooltip == null)
            {
                _currentTooltip = new ToolTip
                {
                    Background = System.Windows.Media.Brushes.LightYellow,
                    BorderBrush = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    FontSize = 12
                };
            }

            var title = graphic.Attributes["Title"]?.ToString() ?? "No Title";
            var description = graphic.Attributes["Description"]?.ToString() ?? "";
            var category = graphic.Attributes["Category"]?.ToString() ?? "";
            var createdTime = graphic.Attributes["CreatedTime"]?.ToString() ?? "";
            var coords = $"Coordinates: ({graphic.Attributes["X"]}, {graphic.Attributes["Y"]})";

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            if (!string.IsNullOrEmpty(description))
            {
                content.Children.Add(new TextBlock
                {
                    Text = description,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            content.Children.Add(new TextBlock
            {
                Text = $"Category: {category}",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray
            });

            content.Children.Add(new TextBlock
            {
                Text = coords,
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray
            });

            content.Children.Add(new TextBlock
            {
                Text = $"Created: {createdTime}",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray
            });

            _currentTooltip.Content = content;
            _currentTooltip.IsOpen = true;
            _currentTooltip.PlacementTarget = _mapView;
            _currentTooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            _currentTooltip.HorizontalOffset = screenPoint.X + 10;
            _currentTooltip.VerticalOffset = screenPoint.Y - 10;
        }

        private void HideTooltip()
        {
            if (_currentTooltip != null)
            {
                _currentTooltip.IsOpen = false;
            }
        }

        #endregion

        #region Data Persistence

        private async void LoadAnnotationsAsync()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = await File.ReadAllTextAsync(_dataFilePath);
                    _annotations = JsonConvert.DeserializeObject<List<AnnotationModel>>(json)
                                  ?? new List<AnnotationModel>();

                    // Display saved annotations
                    foreach (var annotation in _annotations)
                    {
                        AddGraphicToMap(annotation);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data: {ex.Message}");
                _annotations = new List<AnnotationModel>();
            }
        }

        private async Task SaveAnnotationsAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_annotations, Formatting.Indented);
                await File.WriteAllTextAsync(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save data: {ex.Message}");
            }
        }

        private void ExportData()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    DefaultExt = "json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = JsonConvert.SerializeObject(_annotations, Formatting.Indented);
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show($"Data has been exported to: {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}");
            }
        }

        #endregion

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
        public void Execute(object parameter) => _execute((T)parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
