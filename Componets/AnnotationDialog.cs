using HouseWithoutCars.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace HouseWithoutCars.Componets
{
    public partial class AnnotationDialog : Window
    {
        public string AnnotationTitle { get; private set; }
        public string AnnotationDescription { get; private set; }
        public string AnnotationCategory { get; private set; }

        private TextBox _titleTextBox;
        private TextBox _descriptionTextBox;
        private ComboBox _categoryComboBox;

        public AnnotationDialog(AnnotationModel existingAnnotation = null)
        {
            InitializeDialog();

            if (existingAnnotation != null)
            {
                _titleTextBox.Text = existingAnnotation.Title;
                _descriptionTextBox.Text = existingAnnotation.Description;
                _categoryComboBox.Text = existingAnnotation.Category;
                Title = "Title";
            }
        }

        private void InitializeDialog()
        {
            Title = "Add annotation";
            Width = 400;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            for (int i = 0; i < 6; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleLabel = new Label { Content = "Title:", Margin = new Thickness(10, 10, 10, 0) };
            Grid.SetRow(titleLabel, 0);
            grid.Children.Add(titleLabel);

            _titleTextBox = new TextBox { Margin = new Thickness(10, 0, 10, 10) };
            Grid.SetRow(_titleTextBox, 1);
            grid.Children.Add(_titleTextBox);

            var categoryLabel = new Label { Content = "Category:", Margin = new Thickness(10, 0, 10, 0) };
            Grid.SetRow(categoryLabel, 2);
            grid.Children.Add(categoryLabel);

            _categoryComboBox = new ComboBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                IsEditable = true
            };
            _categoryComboBox.Items.Add("Most Important");
            _categoryComboBox.Items.Add("Important");
            _categoryComboBox.Items.Add("Medium");
            _categoryComboBox.Items.Add("Info");
            _categoryComboBox.SelectedIndex = 0;
            Grid.SetRow(_categoryComboBox, 3);
            grid.Children.Add(_categoryComboBox);

            var descLabel = new Label { Content = "Description:", Margin = new Thickness(10, 0, 10, 0) };
            Grid.SetRow(descLabel, 4);
            grid.Children.Add(descLabel);

            _descriptionTextBox = new TextBox
            {
                Margin = new Thickness(10, 0, 10, 10),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 80
            };
            Grid.SetRow(_descriptionTextBox, 5);
            grid.Children.Add(_descriptionTextBox);

            // button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 0, 0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 0, 0)
            };
            okButton.Click += OkButton_Click;

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 6);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_titleTextBox.Text))
            {
                MessageBox.Show("Please Enter Title");
                return;
            }

            AnnotationTitle = _titleTextBox.Text.Trim();
            AnnotationDescription = _descriptionTextBox.Text.Trim();
            AnnotationCategory = _categoryComboBox.Text?.Trim() ?? "Default";

            DialogResult = true;
        }
    }
}
