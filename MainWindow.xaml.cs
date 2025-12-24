using System.Diagnostics;
using System.Text;
using System.Windows;
using EZPC.Models;
using EZPC.Services;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;

namespace EZPC
{
    public partial class MainWindow : FluentWindow
    {
        private readonly SystemScanner _scanner;
        private readonly VersionChecker _versionChecker;
        private HardwareInfo? _lastScan;

        public MainWindow()
        {
            InitializeComponent();
            _scanner = new SystemScanner();
            _versionChecker = new VersionChecker();
            
            Loaded += async (s, e) => await RunScan();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RunScan();

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScan == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== EZPC System Report ===");
            sb.AppendLine();
            sb.AppendLine($"CPU: {_lastScan.CpuName}");
            sb.AppendLine($"Cores: {_lastScan.CpuCores} | Threads: {_lastScan.CpuThreads}");
            sb.AppendLine();
            sb.AppendLine($"GPU: {_lastScan.GpuName}");
            sb.AppendLine($"Driver: {_lastScan.GpuDriverVersion}");
            sb.AppendLine();
            sb.AppendLine($"RAM: {_lastScan.TotalRamGB} GB");
            sb.AppendLine();
            sb.AppendLine("Storage:");
            foreach (var drive in _lastScan.Drives)
            {
                sb.AppendLine($"  {drive.DriveLetter} ({drive.MediaType}) - {drive.FreeSpaceGB}GB free of {drive.CapacityGB}GB");
                sb.AppendLine($"    {drive.Name}");
            }

            Clipboard.SetText(sb.ToString());
            
            var originalText = StatusText.Text;
            StatusText.Text = "Copied to clipboard!";
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, e) => { StatusText.Text = originalText; timer.Stop(); };
            timer.Start();
        }

        private async Task RunScan()
        {
            RefreshButton.IsEnabled = false;
            StatusText.Text = "Scanning...";

            try
            {
                _lastScan = await _scanner.ScanSystemAsync();
                
                // Update System Specs
                CpuSpecText.Text = Truncate(_lastScan.CpuName, 40);
                CpuCoresText.Text = $"{_lastScan.CpuCores} Cores / {_lastScan.CpuThreads} Threads";
                
                GpuSpecText.Text = Truncate(_lastScan.GpuName, 40);
                GpuDriverText.Text = $"Driver: {_lastScan.GpuDriverVersion}";
                
                RamSpecText.Text = $"{_lastScan.TotalRamGB} GB";
                
                // Storage summary
                if (_lastScan.Drives.Count > 0)
                {
                    var totalFree = _lastScan.Drives.Sum(d => d.FreeSpaceGB);
                    var totalCap = _lastScan.Drives.Sum(d => d.CapacityGB);
                    StorageSpecText.Text = $"{totalFree} GB free of {totalCap} GB";
                }
                
                // Get and display recommendations
                var components = _versionChecker.GetRecommendations(_lastScan);
                DisplayComponents(components);

                StatusText.Text = $"Last scan: {DateTime.Now:h:mm tt}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private void DisplayComponents(List<ComponentInfo> components)
        {
            ComponentsPanel.Children.Clear();

            foreach (var comp in components)
            {
                var card = CreateComponentCard(comp);
                ComponentsPanel.Children.Add(card);
            }
        }

        private UIElement CreateComponentCard(ComponentInfo comp)
        {
            var expander = new CardExpander
            {
                Margin = new Thickness(0, 0, 0, 8),
                IsExpanded = comp.Category == ComponentCategory.GPU
            };

            var headerPanel = new StackPanel();
            headerPanel.Children.Add(new TextBlock
            {
                Text = comp.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = comp.Subtitle,
                FontSize = 12,
                Opacity = 0.65,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 550
            });

            expander.Header = headerPanel;

            expander.Icon = new SymbolIcon
            {
                Symbol = comp.Category switch
                {
                    ComponentCategory.GPU => SymbolRegular.VideoClip24,
                    ComponentCategory.CPU => SymbolRegular.Board24,
                    ComponentCategory.Storage => SymbolRegular.Storage24,
                    _ => SymbolRegular.Info24
                }
            };

            var contentPanel = new StackPanel { Margin = new Thickness(4, 8, 4, 4) };

            foreach (var line in comp.Description.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) 
                {
                    contentPanel.Children.Add(new System.Windows.Controls.Separator { Opacity = 0, Height = 6 });
                    continue;
                }
                
                contentPanel.Children.Add(new TextBlock
                {
                    Text = line,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Opacity = line.StartsWith("⚠️") || line.StartsWith("📊") || line.StartsWith("⚡") || line.StartsWith("💾") || line.StartsWith("💡") ? 0.9 : 0.7,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }

            if (!string.IsNullOrEmpty(comp.ExtraInfo))
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = comp.ExtraInfo,
                    FontSize = 11,
                    Opacity = 0.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0)
                });
            }

            if (comp.IsActionable && !string.IsNullOrEmpty(comp.ActionUrl))
            {
                var btn = new Wpf.Ui.Controls.Button
                {
                    Content = comp.ActionText,
                    Icon = new SymbolIcon { Symbol = SymbolRegular.Open24 },
                    Appearance = ControlAppearance.Primary,
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 10, 0, 0)
                };
                btn.Click += (s, e) => OpenUrl(comp.ActionUrl);
                contentPanel.Children.Add(btn);
            }

            expander.Content = contentPanel;
            return expander;
        }

        private static string Truncate(string? text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "Unknown";
            return text.Length <= max ? text : text[..(max - 1)] + "…";
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }
    }
}