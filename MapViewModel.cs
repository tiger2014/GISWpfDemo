using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.UtilityNetworks;
using HouseWithoutCars.Componets;
using HouseWithoutCars.Model;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HouseWithoutCars
{
    public partial class MapViewModel : ObservableObject
    {
        [ObservableProperty]
        private Map _map;

        [ObservableProperty]
        private ObservableCollection<GraphicsOverlay> _graphicsOverlays;

        private GraphicsOverlay _annotationsOverlay;
        private List<AnnotationModel> _annotations = new List<AnnotationModel>();
        private readonly string _dataFilePath = "map_annotations.json";
        private MapView _mapView; // Need a MapView reference to handle events

        private UtilityNetwork _utilityNetwork;
        private GraphicsOverlay _associationsOverlay;
        private ListView _legend;

        public MapViewModel()
        {
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            LoadEntryMap();
            LoadAnnotationsAsync();
            //LoadUtilityNetworkMap();
        }

        // set MapView reference
        public void SetMapView(MapView mapView, ListView legend)
        {
            if (_mapView != null)
            {
                // Remove old event handlers
                _mapView.MouseRightButtonDown -= OnMapViewRightClick;
                _mapView.MouseMove -= OnMapViewMouseMove;
                _mapView.MouseLeave -= OnMapViewMouseLeave;
            }

            _mapView = mapView;
            _legend = legend;

            if (_mapView != null)
            {
                if (_mapView.GraphicsOverlays == null)
                    _mapView.GraphicsOverlays = new GraphicsOverlayCollection();
                else
                    _mapView.GraphicsOverlays.Clear();
                foreach (var overlay in GraphicsOverlays)
                    _mapView.GraphicsOverlays.Add(overlay);
                GraphicsOverlays.CollectionChanged += (s, e) =>
                {
                    if (e.NewItems != null)
                        foreach (GraphicsOverlay o in e.NewItems)
                            _mapView.GraphicsOverlays.Add(o);

                    if (e.OldItems != null)
                        foreach (GraphicsOverlay o in e.OldItems)
                            _mapView.GraphicsOverlays.Remove(o);
                };

                // add new event handlers
                _mapView.MouseRightButtonDown += OnMapViewRightClick;
                _mapView.MouseMove += OnMapViewMouseMove;
                _mapView.MouseLeave += OnMapViewMouseLeave;

                _mapView.NavigationCompleted += OnMapViewViewpointChanged;
            }
        }

        private async void OnMapViewViewpointChanged(object? sender, EventArgs e)
        {
            if (_utilityNetwork != null)
            {
                await AddAssociations();
            }
        }

        [RelayCommand]
        private void Entry()
        {
            LoadEntryMap();
        }

        [RelayCommand]
        private async Task UtilityNetwork()
        {
            await LoadUtilityNetworkMap();
        }

        [RelayCommand]
        private async Task ClearAllAnnotationsAsync()
        {
            await ClearAllAnnotations();
        }

        [RelayCommand]
        private void ExportAnnotations()
        {
            ExportData();
        }

        private void LoadEntryMap()
        {
            Map = new Map(SpatialReferences.WebMercator)
            {
                Basemap = new Basemap(BasemapStyle.ArcGISStreets)
            };

            Envelope texazExtent = new Envelope(-106.65, 25.84, -93.51, 36.5, SpatialReferences.Wgs84);
            Map.InitialViewpoint = new Viewpoint(texazExtent);

            // add TX layer
            try
            {
                var shapefileFeatureTable = new ShapefileFeatureTable("C:\\Develope\\GIS\\HouseWithoutCars\\Data\\State_Agency_Lands_3232346673015478260\\State_Agency_Lands.shp");
                var featureLayer = new FeatureLayer(shapefileFeatureTable);
                Map.OperationalLayers.Add(featureLayer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load shapefile: {ex.Message}");
            }

            // Initialize GraphicsOverlays
            GraphicsOverlays = new ObservableCollection<GraphicsOverlay>();

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
            GraphicsOverlays.Add(originalOverlay);

            // Create a dedicated layer for annotations
            _annotationsOverlay = new GraphicsOverlay { Id = "Annotations" };
            GraphicsOverlays.Add(_annotationsOverlay);

            // Handle map load status
            _map.LoadStatusChanged += (s, e) =>
            {
                if (e.Status == Esri.ArcGISRuntime.LoadStatus.FailedToLoad)
                {
                    Debug.WriteLine($"Map failed to load, error: {_map.LoadError?.Message}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Map failed to load, error: {_map.LoadError?.Message}");
                    });
                }
            };
        }

        private async Task LoadUtilityNetworkMap()
        {
            // As of ArcGIS Enterprise 10.8.1, using utility network functionality requires a licensed user. The following login for the sample server is licensed to perform utility network operations.
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(async (info) =>
            {
                try
                {
                    // WARNING: Never hardcode login information in a production application. This is done solely for the sake of the sample.
                    string sampleServer7User = "viewer01";
                    string sampleServer7Pass = "I68VGU^nMurF";

                    return await AccessTokenCredential.CreateAsync(info.ServiceUri, sampleServer7User, sampleServer7Pass);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return null;
                }
            });

            try
            {
                // Create the map.
                Map = new Map(new Uri("https://sampleserver7.arcgisonline.com/portal/home/item.html?id=be0e4637620a453584118107931f718b"));
                await _map.LoadAsync();

                // Get the utility network from the map.
                _utilityNetwork = _map.UtilityNetworks.FirstOrDefault();
                await _utilityNetwork.LoadAsync();

                // Create a graphics overlay for associations.
                _associationsOverlay = new GraphicsOverlay();
                GraphicsOverlays.Add(_associationsOverlay);

                // Symbols for the associations.
                Symbol attachmentSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Dot, System.Drawing.Color.Green, 5d);
                Symbol connectivitySymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Dot, System.Drawing.Color.Red, 5d);

                // Create a renderer for the associations.
                var attachmentValue = new UniqueValue("Attachment", string.Empty, attachmentSymbol, UtilityAssociationType.Attachment.ToString());
                var connectivityValue = new UniqueValue("Connectivity", string.Empty, connectivitySymbol, UtilityAssociationType.Connectivity.ToString());
                _associationsOverlay.Renderer = new UniqueValueRenderer(new List<string> { "AssociationType" }, new List<UniqueValue> { attachmentValue, connectivityValue }, string.Empty, null);

                // Populate the legend in the UI.
                Dictionary<UtilityAssociationType, System.Windows.Media.ImageSource> legend;
                legend = new Dictionary<UtilityAssociationType, System.Windows.Media.ImageSource>();

                RuntimeImage attachmentSwatch = await attachmentSymbol.CreateSwatchAsync();
                legend[UtilityAssociationType.Attachment] = await attachmentSwatch?.ToImageSourceAsync();

                RuntimeImage connectSwatch = await connectivitySymbol.CreateSwatchAsync();
                legend[UtilityAssociationType.Connectivity] = await connectSwatch?.ToImageSourceAsync();

                Viewpoint InitialViewpoint = new Viewpoint(new MapPoint(-9812698.37297436, 5131928.33743317, SpatialReferences.WebMercator), 22d);
                _legend.ItemsSource = legend;

                // Set the starting viewpoint.
                await _mapView.SetViewpointAsync(InitialViewpoint);

                // Add the associations in the starting viewpoint.
                _ = AddAssociations();

                await PrintFeature(_map);

                // Handle map load status
                Map.LoadStatusChanged += (s, e) =>
                {
                    if (e.Status == Esri.ArcGISRuntime.LoadStatus.FailedToLoad)
                    {
                        Debug.WriteLine($"Map failed to load, error: {_map.LoadError?.Message}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Map failed to load, error: {_map.LoadError?.Message}");
                        });
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddAssociations()
        {
            try
            {
                // Check if the current viewpoint is outside of the max scale.
                if (_mapView.GetCurrentViewpoint(ViewpointType.CenterAndScale)?.TargetScale >= 2000)
                {
                    return;
                }

                // Check if the current viewpoint has an extent.
                Envelope extent = _mapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry)?.TargetGeometry?.Extent;
                if (extent == null)
                {
                    return;
                }

                // Get all of the associations in extent of the viewpoint.
                IEnumerable<UtilityAssociation> associations = await _utilityNetwork.GetAssociationsAsync(extent);
                foreach (UtilityAssociation association in associations)
                {
                    // Check if the graphics overlay already contains the association.
                    if (_associationsOverlay.Graphics.Any(g => g.Attributes.ContainsKey("GlobalId") && (Guid)g.Attributes["GlobalId"] == association.GlobalId))
                    {
                        continue;
                    }

                    // Add a graphic for the association.
                    Graphic graphic = new Graphic(association.Geometry);
                    graphic.Attributes["GlobalId"] = association.GlobalId;
                    graphic.Attributes["AssociationType"] = association.AssociationType.ToString();
                    _associationsOverlay.Graphics.Add(graphic);
                }
            }

            // This is thrown when there are too many associations in the extent.
            catch (TooManyAssociationsException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnMapViewRightClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var screenPoint = e.GetPosition(_mapView);
                // Convert the screen point to map coordinates
                var mapPoint = _mapView.ScreenToLocation(screenPoint);

                if (mapPoint != null)
                {
                    await AddAnnotationAtLocation(mapPoint);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnMapViewRightClick: {ex.Message}");
                MessageBox.Show($"Failed to add annotation: {ex.Message}");
            }
        }

        #region Event Handlers

        private ToolTip _currentTooltip;

        private async void OnMapViewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_annotationsOverlay?.Graphics?.Count > 0)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnMapViewMouseMove: {ex.Message}");
            }
        }

        private void OnMapViewMouseLeave(object sender, MouseEventArgs e)
        {
            HideTooltip();
        }

        #endregion Event Handlers

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
                        X = wgs84Location.X,
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
                    Style = SimpleMarkerSymbolStyle.Diamond,
                    Color = color,
                    Size = 20,
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

                Debug.WriteLine($"AddGraphicToMap Added graphic at: {wgs84Point.X}, {wgs84Point.Y}");
                Debug.WriteLine($"AddGraphicToMap Total graphics in overlay: {_annotationsOverlay.Graphics.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding graphic: {ex.Message}");
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

        private async Task ClearAllAnnotations()
        {
            if (MessageBox.Show("Are you sure you want to clear all annotations?", "Confirm Clear",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _annotations.Clear();
                _annotationsOverlay?.Graphics.Clear();
                await SaveAnnotationsAsync();
                MessageBox.Show("All annotations have been cleared!");
            }
        }

        #endregion Annotation Management

        #region Tooltip Management

        private void ShowTooltip(Graphic graphic, Point screenPoint)
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing tooltip: {ex.Message}");
            }
        }

        private void HideTooltip()
        {
            try
            {
                if (_currentTooltip != null)
                {
                    _currentTooltip.IsOpen = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error hiding tooltip: {ex.Message}");
            }
        }

        #endregion Tooltip Management

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

                    // Wait for map to be initialized before adding graphics
                    if (_annotationsOverlay != null)
                    {
                        // Display saved annotations
                        foreach (var annotation in _annotations)
                        {
                            AddGraphicToMap(annotation);
                        }
                    }
                }
                else
                {
                    _annotations = new List<AnnotationModel>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load annotations: {ex.Message}");
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
                Debug.WriteLine($"Failed to save annotations: {ex.Message}");
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

        #endregion Data Persistence

        // dispose
        public void Cleanup()
        {
            if (_mapView != null)
            {
                _mapView.MouseRightButtonDown -= OnMapViewRightClick;
                _mapView.MouseMove -= OnMapViewMouseMove;
                _mapView.MouseLeave -= OnMapViewMouseLeave;
            }
            HideTooltip();
        }

        // Full screen: zoom to the full extent of all layers
        [RelayCommand]
        private async Task FullScreenAsync()
        {
            if (_mapView != null && _map != null)
            {
                // If the map has layers, zoom to the extent of all layers
                if (_map.OperationalLayers.Count > 0)
                {
                    try
                    {
                        var fullExtent = GeometryEngine.Union(
                            _map.OperationalLayers
                                .OfType<FeatureLayer>()
                                .Where(l => l.FullExtent != null)
                                .Select(l => l.FullExtent)
                        );

                        if (fullExtent != null)
                        {
                            await _mapView.SetViewpointGeometryAsync(fullExtent, 50);
                        }
                    }
                    catch
                    {
                        await ResetViewAsync();
                    }
                }
                else
                {
                    await ResetViewAsync();
                }
            }
        }

        // Reset view: Return to the Texas range set when LoadEntryMap
        [RelayCommand]
        private async Task ResetViewAsync()
        {
            if (_mapView != null && _map != null)
            {
                var texasExtent = new Envelope(-106.65, 25.84, -93.51, 36.5, SpatialReferences.Wgs84);
                await _mapView.SetViewpointGeometryAsync(texasExtent, 50);
            }
        }

        /// <summary>
        /// print layer and feature
        /// </summary>
        /// <param name="map"></param>
        public async Task PrintFeature(Map map)
        {
            var features = new List<string>();
            if (map == null) return;
            foreach (var layer in map.OperationalLayers)
            {
                var typeName = layer.GetType().Name;
                Debug.WriteLine($"layer name: {layer.Name}, {layer.Description}, {typeName}");
                if (layer is FeatureLayer featureLayer)
                {
                    var table = featureLayer.FeatureTable;

                    // query all feature
                    var queryParams = new QueryParameters
                    {
                        WhereClause = "1=1"
                    };

                    var result = await table.QueryFeaturesAsync(queryParams);

                    foreach (var feature in result)
                    {
                        Debug.WriteLine($"feature: {feature.FeatureTable.DisplayName}");
                        if (!features.Any(s => s == feature.FeatureTable.DisplayName) && !string.IsNullOrWhiteSpace(feature.FeatureTable.DisplayName))
                        {
                            features.Add(feature.FeatureTable.DisplayName);
                        }
                    }
                }
            }

            if (features.Count > 0)
            {
                Debug.WriteLine("All feature type:");
                foreach (var item in features)
                {
                    Debug.WriteLine(item);
                }
            }
        }
    }
}