using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using HouseWithoutCars.Componets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
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
            // 鼠标移动事件（悬停显示提示）
            _mapView.MouseMove += OnMapViewMouseMove;

            // 鼠标离开事件
            _mapView.MouseLeave += OnMapViewMouseLeave;
        }

        // 启用/禁用添加模式
        public void SetAddingMode(bool enabled)
        {
            _isAddingMode = enabled;
            _mapView.Cursor = enabled ? Cursors.Cross : Cursors.Arrow;
        }

        private async Task AddAnnotationAtLocation(MapPoint location)
        {
            try
            {
                // 弹出对话框获取用户输入
                var dialog = new AnnotationDialog();
                if (dialog.ShowDialog() == true)
                {
                    var annotation = new AnnotationModel
                    {
                        X = location.X,
                        Y = location.Y,
                        Title = dialog.AnnotationTitle,
                        Description = dialog.AnnotationDescription,
                        Category = dialog.AnnotationCategory
                    };

                    // 添加到数据集合
                    _annotationModel.Add(annotation);

                    // 在地图上显示
                    AddGraphicToMap(annotation);

                    // 保存数据
                    await SaveDataAsync();

                    MessageBox.Show($"标注 '{annotation.Title}' 已添加！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加标注失败: {ex.Message}");
            }
        }

        private void AddGraphicToMap(AnnotationModel annotation)
        {
            var point = new MapPoint(annotation.X, annotation.Y, SpatialReferences.Wgs84);

            // 根据分类设置不同颜色
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

            // 创建属性字典，用于提示显示
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
                "重要" => System.Drawing.Color.Red,
                "警告" => System.Drawing.Color.Orange,
                "信息" => System.Drawing.Color.Blue,
                "完成" => System.Drawing.Color.Green,
                _ => System.Drawing.Color.Purple
            };
        }

        private async void OnMapViewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                // 识别鼠标位置下的图形
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
                // 忽略识别错误
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

            // 构建提示内容
            var title = graphic.Attributes["Title"]?.ToString() ?? "无标题";
            var description = graphic.Attributes["Description"]?.ToString() ?? "";
            var category = graphic.Attributes["Category"]?.ToString() ?? "";
            var createdTime = graphic.Attributes["CreatedTime"]?.ToString() ?? "";
            var coords = $"坐标: ({graphic.Attributes["X"]}, {graphic.Attributes["Y"]})";

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
                Text = $"分类: {category}",
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
                Text = $"创建: {createdTime}",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray
            });

            _currentTooltip.Content = content;
            _currentTooltip.IsOpen = true;

            // 设置提示位置
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

                    // 显示编辑/删除选项
                    ShowAnnotationContextMenu(annotationId, screenPoint);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检查标注失败: {ex.Message}");
            }
        }

        private void ShowAnnotationContextMenu(string annotationId, Point screenPoint)
        {
            var contextMenu = new ContextMenu();

            var editItem = new MenuItem { Header = "编辑" };
            editItem.Click += (s, e) => EditAnnotation(annotationId);

            var deleteItem = new MenuItem { Header = "删除" };
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
            var annotation = _annotationModel.FirstOrDefault(a => a.Id == annotationId);
            if (annotation != null)
            {
                var dialog = new AnnotationDialog(annotation);
                if (dialog.ShowDialog() == true)
                {
                    annotation.Title = dialog.AnnotationTitle;
                    annotation.Description = dialog.AnnotationDescription;
                    annotation.Category = dialog.AnnotationCategory;

                    RefreshGraphics();
                    await SaveDataAsync();

                    MessageBox.Show("标注已更新！");
                }
            }
        }

        private async void DeleteAnnotation(string annotationId)
        {
            if (MessageBox.Show("确定要删除这个标注吗？", "确认删除",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _annotationModel.RemoveAll(a => a.Id == annotationId);
                RefreshGraphics();
                await SaveDataAsync();

                MessageBox.Show("标注已删除！");
            }
        }

        private void RefreshGraphics()
        {
            _annotationsOverlay.Graphics.Clear();
            foreach (var annotation in _annotationModel)
            {
                AddGraphicToMap(annotation);
            }
        }

        // 数据保存
        private async Task SaveDataAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_annotationModel, Formatting.Indented);
                await File.WriteAllTextAsync(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存数据失败: {ex.Message}");
            }
        }

        // 数据加载
        private async void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = await File.ReadAllTextAsync(_dataFilePath);
                    _annotationModel = JsonConvert.DeserializeObject<List<AnnotationModel>>(json)
                                  ?? new List<AnnotationModel>();

                    // 显示已保存的标注
                    foreach (var annotation in _annotationModel)
                    {
                        AddGraphicToMap(annotation);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}");
                _annotationModel = new List<AnnotationModel>();
            }
        }

        // 导出数据
        public void ExportData(string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_annotationModel, Formatting.Indented);
                File.WriteAllText(filePath, json);
                MessageBox.Show($"数据已导出到: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}");
            }
        }

        // 获取所有标注
        public List<AnnotationModel> GetAllAnnotations()
        {
            return _annotationModel.ToList();
        }

        // 清除所有标注
        public async Task ClearAllAnnotations()
        {
            if (MessageBox.Show("确定要清除所有标注吗？", "确认清除",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _annotationModel.Clear();
                _annotationsOverlay.Graphics.Clear();
                await SaveDataAsync();
                MessageBox.Show("所有标注已清除！");
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