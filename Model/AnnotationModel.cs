using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Formatting = Newtonsoft.Json.Formatting;

namespace HouseWithoutCars.Model
{
    public class MapAnnotationManager
    {
        private MapView _mapView;
        private GraphicsOverlay _annotationsOverlay;
        private List<AnnotationModel> _annotationModel;
        private string _dataFilePath;
        private ToolTip _currentTooltip;
        private bool _isAddingMode = false;

        public MapAnnotationManager(MapView mapView, string dataFilePath = "map_annotations.json")
        {
            _mapView = mapView;
            _dataFilePath = dataFilePath;
            _annotationModel = new List<AnnotationModel>();

            InitializeOverlay();
            LoadData();
            SetupEventHandlers();
        }

        private void InitializeOverlay()
        {
            _annotationsOverlay = new GraphicsOverlay
            {
                Id = "Annotations"
            };
            _mapView.GraphicsOverlays.Add(_annotationsOverlay);
        }

        private void SetupEventHandlers()
        {
            _mapView.MouseMove += OnMapViewMouseMove;

            _mapView.MouseLeave += OnMapViewMouseLeave;
        }

        private void AddGraphicToMap(AnnotationModel annotation)
        {
            var point = new MapPoint(annotation.X, annotation.Y, SpatialReferences.Wgs84);

            var color = GetColorByCategory(annotation.Category);

            var symbol = new SimpleMarkerSymbol
            {
                Style = SimpleMarkerSymbolStyle.Circle,
                Color = color,
                Size = 12,
                Outline = new SimpleLineSymbol
                {
                    Style = SimpleLineSymbolStyle.Solid,
                    Color = System.Drawing.Color.White,
                    Width = 2
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

            var graphic = new Graphic(point, attributes, symbol);
            _annotationsOverlay.Graphics.Add(graphic);
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
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void OnMapViewMouseLeave(object sender, MouseEventArgs e)
        {
            HideTooltip();
        }

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

            var title = graphic.Attributes["Title"]?.ToString() ?? "default";
            var description = graphic.Attributes["Description"]?.ToString() ?? "";
            var category = graphic.Attributes["Category"]?.ToString() ?? "";
            var createdTime = graphic.Attributes["CreatedTime"]?.ToString() ?? "";
            var coords = $"Location: ({graphic.Attributes["X"]}, {graphic.Attributes["Y"]})";

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
                Text = $"Create: {createdTime}",
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

        private async Task SaveDataAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_annotationModel, Formatting.Indented);
                await File.WriteAllTextAsync(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save data failed: {ex.Message}");
            }
        }

        private async void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = await File.ReadAllTextAsync(_dataFilePath);
                    _annotationModel = JsonConvert.DeserializeObject<List<AnnotationModel>>(json)
                                  ?? new List<AnnotationModel>();

                    foreach (var annotation in _annotationModel)
                    {
                        AddGraphicToMap(annotation);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load data failed: {ex.Message}");
                _annotationModel = new List<AnnotationModel>();
            }
        }
    }

    public class AnnotationModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double X { get; set; }
        public double Y { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public string Category { get; set; } = "Default";
    }
}