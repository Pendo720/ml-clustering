﻿using KmsLibraryCode;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Clusterer
{
    public partial class MainWindow : Window
    {
        private readonly ILogger _logger;
        private readonly float _crossLength = 5;
        private readonly string _fileDirectory = Environment.CurrentDirectory;
        private float _actualWidth, _actualHeight, _xOffset;
        private List<string> _categories;
        public bool HasCluster { get; set; }

        List<Feature<Field>> _features;
        MLDataPipeline<Feature<Field>> _pipeline;
        KmsAlgorithm<Field> _kmsAlgorithm;
        public List<PlotFeatures> PlotSettings { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            _logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(path: _fileDirectory + "runlog.log", 
                            restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning, 
                            rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
                .CreateLogger();
            _logger.Warning("Application started");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Background = Brushes.Gray;
            PlotSettings = new List<PlotFeatures>();
            _actualWidth = (float)canvas.ActualWidth;
            _actualHeight = (float)canvas.ActualHeight;
            _xOffset = (_actualWidth - _actualHeight) / 2;
            canvas.Background = Brushes.White;
            HasCluster = false;
        }

        private void LoadData_Click(object sender, RoutedEventArgs e)
        {
            var filePath = string.Empty;
            if (ShowOpenDialog(ref filePath) && filePath != string.Empty)
            {
                _kmsAlgorithm?.Clusters.Clear();
                _categories = new List<string>();
                _features = MLDataPipeline<Feature<Field>>.ImportCSV(filePath, ref _categories);
                PlotConfiguration(_features, true);

                _pipeline = new MLDataPipeline<Feature<Field>>(_features);
                canvas.Children.Clear();
                exportClusters.IsEnabled = false;
                runClustering.IsEnabled = true;
                _logger.Information("Data loaded...");

                _pipeline.Training.ForEach(p => DrawFeature(p, Brushes.Blue));
                var pathBits = filePath.Split('\\');
                txtfileName.Content = pathBits[pathBits.Length - 1];
                validateBtn.Visibility = Visibility.Hidden;
            }
        }

        private void PlotConfiguration(List<Feature<Field>> m, bool enabled)
        {
            PlotSettings.Clear();
            new List<string>() { "Horizontal", "Vertical", "Strength" }
            .Select((k, i) => (k, i)).ToList().ForEach(d =>
            {
                var p = new PlotFeatures(d.k);
                p.SelectionIndex = d.i;
                p.Options = m.ToArray()[0].Fields.Select(t => t.Name.Trim()).ToList();
                xselection.ItemsSource = p.Options;
                PlotSettings.Add(p);
            });

            var c = PlotSettings.ToArray()[0];
            c.SelectionIndex = 0;
            xselection.ItemsSource = c.Options;
            xselection.SelectedIndex = c.SelectionIndex;

            c = PlotSettings.ToArray()[1];
            c.SelectionIndex = 1;
            yselection.ItemsSource = c.Options;
            yselection.SelectedIndex = c.SelectionIndex;

            c = PlotSettings.ToArray()[2];
            c.SelectionIndex = 2;
            strengthselection.ItemsSource = c.Options;
            strengthselection.SelectedIndex = c.SelectionIndex;

            xselection.IsEnabled = enabled;
            yselection.IsEnabled = enabled;
            strengthselection.IsEnabled = enabled;
        }

        private void RunClustering_Click(object sender, RoutedEventArgs e)
        {
            _kmsAlgorithm = new KmsAlgorithm<Field>(_pipeline.Training, _categories);
            if (canvas.Children.Count != 0)
            {
                canvas.Children.Clear();
            }
            _pipeline.Training.ForEach(p => DrawFeature(p, Brushes.Blue));

            _kmsAlgorithm.runIterations();
            exportClusters.IsEnabled = true;
            _logger.Information("Algorithm ran...");

            SolidColorBrush[] all = { Brushes.Red, Brushes.Green, Brushes.Blue };

            foreach (var C in _kmsAlgorithm.Clusters.Select((k, i) => (k, i)))
            {
                DrawCentroid(C.k, all[C.i]);
            }
            clusterList.DataContext = _kmsAlgorithm.Clusters;
            HasCluster = true;
            validateBtn.Visibility = Visibility.Visible;
        }

        private async void ExportClusters_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("Implement exportClusters()...");
            var filePath = string.Empty;
            if (ShowSaveDialog(ref filePath) && filePath != string.Empty)
            {
                await _kmsAlgorithm.ExportClusters(filePath);
            }
        }

        private void ImportClusters_Click(object sender, RoutedEventArgs e)
        {
            var filePath = string.Empty;
            if (ShowOpenDialog(ref filePath, "JSON files (*.json)|*.json") && filePath != string.Empty)
            {
                canvas.Children.Clear();
                _kmsAlgorithm = new KmsAlgorithm<Field>();
                _kmsAlgorithm.ImportClusters(filePath);
                PlotConfiguration(_kmsAlgorithm.Clusters.ToArray()[0].Elements, false);

                SolidColorBrush[] all = { Brushes.Red, Brushes.Green, Brushes.Blue };

                foreach (var C in _kmsAlgorithm.Clusters.Select((k, i) => (k, i)))
                {
                    DrawCentroid(C.k, all[C.i]);
                    C.k.Elements.ForEach(f => DrawFeature(f, all[C.i]));
                }

                _logger.Information("Implement importClusters()...");
                var pathBits = filePath.Split('\\');
                txtfileName.Content = pathBits[pathBits.Length - 1];
                runClustering.IsEnabled = false;
                exportClusters.IsEnabled = false;
                clusterList.DataContext = _kmsAlgorithm.Clusters;
                HasCluster = true;
                validateBtn.Visibility = Visibility.Visible;
            }
        }
        #region drawing

        private void DrawFeature(Feature<Field> p, SolidColorBrush colour)
        {
            SolidColorBrush[] all = { Brushes.Red, Brushes.Green, Brushes.Blue, Brushes.Orange, Brushes.DarkCyan };
            if (_kmsAlgorithm != null) { 
            
                foreach (var C in _kmsAlgorithm?
                                    .Clusters?
                                    .Select((k, i) => (k, i)))
                {
                    colour = C.k.Elements.Contains(p) ? all[C.i] : colour;
                }
            }
            Line hLine = new Line(), vLine = new Line();
            float[] values = p.GetValues().ToArray();
            if (values.Length > 0)
            {
                float x = values[xselection.SelectedIndex],
                    y = values[yselection.SelectedIndex];
                Point point = MapToCanvas(x, y);
                hLine.Stroke = colour;
                vLine.Stroke = colour;

                hLine.X1 = (point.X > _crossLength) ? (point.X - _crossLength) : _crossLength;
                hLine.X2 = point.X + _crossLength;
                hLine.Y1 = point.Y;
                hLine.Y2 = point.Y;
                hLine.StrokeThickness = 1;

                canvas.Children.Add(hLine);
                vLine.X1 = point.X;
                vLine.X2 = point.X;
                vLine.Y1 = (point.Y > _crossLength) ? (point.Y - _crossLength) : _crossLength;
                vLine.Y2 = point.Y + _crossLength;
                canvas.Children.Add(vLine);
            }
        }

        private void DrawCentroid(Cluster<Field> C, SolidColorBrush colour)
        {
            float[] values = C.Centroid.GetValues().ToArray();
            if (values.Length > 0)
            {
                float x = values[xselection.SelectedIndex],
                    y = values[yselection.SelectedIndex];
                Point point = MapToCanvas(x, y);
                Ellipse circle = new Ellipse();

                circle.Width = _crossLength * 2;
                circle.Height = _crossLength * 2;
                circle.Stroke = colour;
                circle.Fill = colour;
                Canvas.SetTop(circle, point.Y - _crossLength);
                Canvas.SetLeft(circle, point.X - _crossLength);
                canvas.Children.Add(circle);

                var txtBlock = new TextBlock();
                txtBlock.Text = C.Label;
                txtBlock.FontSize *= 2;
                Canvas.SetTop(txtBlock, point.Y - 2 * _crossLength);
                Canvas.SetLeft(txtBlock, point.X + 2 * _crossLength);
                canvas.Children.Add(txtBlock);

                C.Elements.ForEach(e =>
                {
                    if (e.Fields.Count > 0)
                    {
                        Line line = new Line();
                        values = e.GetValues().ToArray();
                        line.Stroke = colour;
                        line.StrokeThickness = values[2];
                        float nx = values[xselection.SelectedIndex], ny = values[yselection.SelectedIndex];
                        Point point2 = MapToCanvas(nx, ny);
                        line.X1 = point.X;
                        line.X2 = point2.X;
                        line.Y1 = point.Y;
                        line.Y2 = point2.Y;
                        canvas.Children.Add(line);
                    }
                });
            }
        }
        #endregion

        private Point MapToCanvas(float x, float y)
        {
            x = x * _actualHeight + _xOffset;
            y = y * _actualHeight;
            return new Point(x, y);
        }
        
        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private bool ShowOpenDialog(ref string filePath, string filter = "CSV files (*.csv)|*.csv")
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = _fileDirectory;
            openFileDialog.Filter = filter;
            openFileDialog.FilterIndex = 0;
            openFileDialog.RestoreDirectory = true;
            var showResult = openFileDialog.ShowDialog();
            filePath = openFileDialog.FileName;
            return showResult.Value;
        }

        private void feature_selectionChanged(object sender, EventArgs e)
        {
            if (PlotSettings != null)
            {
                canvas.Children.Clear();
                if (sender == xselection)
                {
                    PlotSettings.ToArray()[0].SelectionIndex = 0;
                }

                if (sender == yselection)
                {
                    PlotSettings.ToArray()[1].SelectionIndex = 1;
                }

                if (sender == strengthselection)
                {
                    PlotSettings.ToArray()[2].SelectionIndex = 2;
                }

                _pipeline?.Training.ForEach(p =>
                {
                    DrawFeature(p, Brushes.Blue);
                });
            }
        }

        private void validateBtn_Click(object sender, RoutedEventArgs e)
        {
            Feature<Field> f = _features.ToArray()[0];
            List<string> labels = f.Fields.Select(d => d.Name).ToList();
            var dialog = new Clustering.CrossValidatorDialog(labels);
            if (true == dialog.ShowDialog())
            {
                f = new Feature<Field>();
                dialog.Fields.ForEach(k => f.Add(new Field(k.Label, k.Value)));
                string sLabel = _kmsAlgorithm.Classify(f).Label;
                MessageBox.Show(f.ToString(), sLabel);
            }
        }

        private bool ShowSaveDialog(ref string filePath, string filter = "JSON files (*.json)|*.json")
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = _fileDirectory;
            saveFileDialog.Filter = filter;
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            var showResult = saveFileDialog.ShowDialog();
            filePath = saveFileDialog.FileName;
            return showResult.Value;
        }
    }

    public class PlotFeatures
    {
        public PlotFeatures(string tag)
        {
            Direction = tag;
            Options = new List<string>();
        }

        public string Direction { get; set; }

        public int SelectionIndex { get; set; }

        public List<string> Options { set; get; }
    }
}
