using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using OBS_Helper.MacOS.Services.Obs;

namespace OBS_Helper.MacOS.Views;

/// <summary>监控页（与 Windows 版 PerformancePage 对齐）：帧率 / 丢帧 / 磁盘预警。</summary>
public partial class MonitorPage : UserControl
{
    private sealed class AlertRow
    {
        public string Title { get; init; } = "";
        public string Detail { get; init; } = "";
        public string Suggestion { get; init; } = "";
        public string? ProblemId { get; init; }
        public bool HasProblem => !string.IsNullOrEmpty(ProblemId);
        public IBrush DotBrush { get; init; } = Brushes.Gray;
    }

    private System.Timers.Timer? _uiTimer;

    public MonitorPage()
    {
        InitializeComponent();
        // 页面销毁后必须释放定时器，否则泄漏；Loaded/Unloaded 可多次触发，
        // 因此每次 Unloaded 释放、Loaded 重建。
        Loaded += (_, _) =>
        {
            if (_uiTimer is null)
            {
                _uiTimer = new System.Timers.Timer(1000);
                _uiTimer.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshFromService);
            }
            _uiTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _uiTimer?.Stop();
            _uiTimer?.Dispose();
            _uiTimer = null;
        };
    }

    private void RefreshFromService()
    {
        var mon = App.Services.Monitor;
        var latest = mon.Latest;
        if (latest is { } s)
        {
            FpsText.Text = s.ActiveFps.ToString("0");
            RenderTimeText.Text = s.FrameRenderTimeMs.ToString("0.0");
            SkipText.Text = $"{s.RenderSkipRatio:P1}";
            DropText.Text = $"{s.StreamDropRatio:P1}";
            DiskText.Text = s.AvailableDiskGb > 0 ? $"{s.AvailableDiskGb:0.0} GB" : "—";
        }

        AlertList.ItemsSource = mon.Alerts.Take(20).Select(a => new AlertRow
        {
            Title = $"[{a.SeverityText}] {a.Title}",
            Detail = $"{a.At:HH:mm:ss} · {a.Detail}",
            Suggestion = a.Suggestion,
            ProblemId = a.ProblemId,
            DotBrush = a.Severity switch
            {
                LogSeverity.Critical or LogSeverity.Error =>
                    (IBrush?)this.FindResource("DangerBrush") ?? Brushes.Red,
                LogSeverity.Warning => (IBrush?)this.FindResource("WarningBrush") ?? Brushes.Orange,
                _ => (IBrush?)this.FindResource("AccentBrush") ?? Brushes.DodgerBlue
            }
        }).ToList();
    }

    private void OnToggleWatch(object? sender, RoutedEventArgs e)
    {
        var mon = App.Services.Monitor;
        if (WatchToggle.IsChecked == true)
        {
            if (!App.Services.Obs.IsConnected)
            {
                WatchToggle.IsChecked = false;
                return;
            }
            mon.Start();
            _uiTimer?.Start();
        }
        else
        {
            mon.SetEnabled(false);
            _ = mon.StopAsync();
        }
    }

    private void OnOpenProblem(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string id)
            (this.GetVisualRoot() as MainWindow)?.OpenProblem(problemId: id);
    }
}
