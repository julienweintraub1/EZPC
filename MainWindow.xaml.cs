using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EZPC.Models;
using EZPC.Services;

namespace EZPC
{
    public partial class MainWindow : Window
    {
        private readonly SystemScanner _scanner;
        private readonly VersionChecker _versionChecker;
        private HardwareInfo _lastScanResults;

        public MainWindow()
        {
            InitializeComponent();
            _scanner = new SystemScanner();
            _versionChecker = new VersionChecker();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            ScanButton.IsEnabled = false;
            StatusText.Text = "Scanning system...";
            SpecsText.Text = "Please wait, scanning hardware...\n\nThis may take 5-10 seconds.";
            UpdatesPanel.Children.Clear();

            try
            {
                // Run the scan
                _lastScanResults = await _scanner.ScanSystemAsync();

                // Display specs
                DisplaySystemSpecs(_lastScanResults);

                // Check for updates
                var recommendations = _versionChecker.CheckForUpdates(_lastScanResults);
                DisplayUpdateRecommendations(recommendations);

                StatusText.Text = $"Scan completed! Found {recommendations.Count} recommendations.";
            }
            catch (Exception ex)
            {
                SpecsText.Text = $"ERROR: {ex.Message}\n\nPlease run as administrator if permission issues occur.";
                StatusText.Text = "Scan failed.";
            }
            finally
            {
                ScanButton.IsEnabled = true;
            }
        }

        private void DisplaySystemSpecs(HardwareInfo results)
        {
            var output = new StringBuilder();
            output.AppendLine("═══════════════════════════════════════════════");
            output.AppendLine("  SYSTEM SCAN COMPLETE");
            output.AppendLine("═══════════════════════════════════════════════");
            output.AppendLine($"Scan Date: {results.ScanDate:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine();

            output.AppendLine("━━━ PROCESSOR ━━━");
            output.AppendLine($"Model: {results.CpuName ?? "Unknown"}");
            output.AppendLine($"Manufacturer: {results.CpuManufacturer ?? "Unknown"}");
            output.AppendLine($"Cores: {results.CpuCores}");
            output.AppendLine($"Threads: {results.CpuThreads}");
            output.AppendLine();

            output.AppendLine("━━━ GRAPHICS CARD ━━━");
            output.AppendLine($"Model: {results.GpuName ?? "Unknown"}");
            output.AppendLine($"Manufacturer: {results.GpuManufacturer ?? "Unknown"}");
            output.AppendLine($"Driver Version: {results.GpuDriverVersion ?? "Unknown"}");
            output.AppendLine($"Driver Date: {results.GpuDriverDate ?? "Unknown"}");
            output.AppendLine();

            output.AppendLine("━━━ MEMORY ━━━");
            output.AppendLine($"Total RAM: {results.TotalRamGB} GB");
            output.AppendLine();

            output.AppendLine("━━━ MOTHERBOARD & BIOS ━━━");
            output.AppendLine($"Manufacturer: {results.MotherboardManufacturer ?? "Unknown"}");
            output.AppendLine($"Model: {results.MotherboardModel ?? "Unknown"}");
            output.AppendLine($"BIOS Version: {results.BiosVersion ?? "Unknown"}");
            output.AppendLine($"BIOS Date: {results.BiosDate ?? "Unknown"}");
            output.AppendLine();

            output.AppendLine("━━━ OPERATING SYSTEM ━━━");
            output.AppendLine($"Version: {results.WindowsVersion ?? "Unknown"}");
            output.AppendLine($"Build: {results.WindowsBuild ?? "Unknown"}");
            output.AppendLine();
            output.AppendLine("═══════════════════════════════════════════════");

            SpecsText.Text = output.ToString();
        }

        private void DisplayUpdateRecommendations(System.Collections.Generic.List<UpdateRecommendation> recommendations)
        {
            UpdatesPanel.Children.Clear();

            if (recommendations == null || recommendations.Count == 0)
            {
                var noUpdates = new TextBlock
                {
                    Text = "No update recommendations available.",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                };
                UpdatesPanel.Children.Add(noUpdates);
                return;
            }

            foreach (var rec in recommendations)
            {
                var card = CreateUpdateCard(rec);
                UpdatesPanel.Children.Add(card);
            }
        }

        private Border CreateUpdateCard(UpdateRecommendation rec)
        {
            var priorityColor = rec.Priority switch
            {
                UpdatePriority.Critical => Color.FromRgb(231, 76, 60),   // Red
                UpdatePriority.High => Color.FromRgb(230, 126, 34),      // Orange
                UpdatePriority.Medium => Color.FromRgb(241, 196, 15),    // Yellow
                UpdatePriority.Low => Color.FromRgb(52, 152, 219),       // Blue
                UpdatePriority.UpToDate => Color.FromRgb(46, 204, 113),  // Green
                _ => Color.FromRgb(149, 165, 166)                        // Gray
            };

            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(priorityColor),
                BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var cardContent = new StackPanel();

            // Header with component name and priority badge
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            var componentName = new TextBlock
            {
                Text = rec.Component,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(componentName);

            var priorityBadge = new Border
            {
                Background = new SolidColorBrush(priorityColor),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 0, 0)
            };
            priorityBadge.Child = new TextBlock
            {
                Text = rec.Priority.ToString().ToUpper(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            header.Children.Add(priorityBadge);

            cardContent.Children.Add(header);

            // Version info
            if (rec.Priority != UpdatePriority.UpToDate)
            {
                var versionInfo = new TextBlock
                {
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                versionInfo.Inlines.Add(new Run("Current: ") { FontWeight = FontWeights.SemiBold });
                versionInfo.Inlines.Add(new Run(rec.CurrentVersion));
                versionInfo.Inlines.Add(new Run("  →  Latest: ") { FontWeight = FontWeights.SemiBold });
                versionInfo.Inlines.Add(new Run(rec.LatestVersion) { Foreground = new SolidColorBrush(priorityColor) });
                cardContent.Children.Add(versionInfo);
            }

            // Description
            var description = new TextBlock
            {
                Text = rec.Description,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            cardContent.Children.Add(description);

            // Instructions (collapsible)
            if (!string.IsNullOrEmpty(rec.Instructions))
            {
                var instructionsExpander = new Expander
                {
                    Header = "Show Update Instructions",
                    FontSize = 12,
                    Margin = new Thickness(0, 5, 0, 10)
                };

                var instructionsText = new TextBlock
                {
                    Text = rec.Instructions,
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 10, 0, 0)
                };
                instructionsExpander.Content = instructionsText;
                cardContent.Children.Add(instructionsExpander);
            }

            // Action button
            if (!string.IsNullOrEmpty(rec.UpdateUrl) && rec.Priority != UpdatePriority.UpToDate)
            {
                var button = new Button
                {
                    Content = "Open Update Page",
                    Background = new SolidColorBrush(priorityColor),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(15, 8, 15, 8),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    FontWeight = FontWeights.SemiBold
                };

                button.Click += (s, e) => OpenUrl(rec.UpdateUrl);
                cardContent.Children.Add(button);
            }

            card.Child = cardContent;
            return card;
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}