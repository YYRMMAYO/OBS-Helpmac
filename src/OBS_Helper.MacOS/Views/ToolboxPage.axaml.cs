using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OBS_Helper.MacOS.Services.Host;
using OBS_Helper.MacOS.Services.Obs;

namespace OBS_Helper.MacOS.Views;

/// <summary>工具箱页（与 Windows 版 ToolboxPage 对齐）：日志 / 备份 / 重置。</summary>
public partial class ToolboxPage : UserControl
{
    public ToolboxPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshLogsAsync();
    }

    private async Task RefreshLogsAsync()
    {
        var logs = await App.Services.Host.ListObsLogsAsync();
        LogFileList.ItemsSource = logs;
    }

    private async void OnOpenLogs(object? sender, RoutedEventArgs e)
        => ToolStatus.Text = (await App.Services.Host.GetEnvironmentAsync()).ObsLogDirectory;

    private async void OnRevealLogs(object? sender, RoutedEventArgs e)
        => await App.Services.Host.RevealInFinderAsync(HostBridge.ObsLogDirectory);

    private async void OnAnalyzeLog(object? sender, RoutedEventArgs e)
    {
        var logs = await App.Services.Host.ListObsLogsAsync();
        var latest = logs.FirstOrDefault();
        if (latest is null)
        {
            ToolStatus.Text = "未找到 OBS 日志文件。请先运行一次 OBS 并复现问题。";
            return;
        }

        AnalyzeBtn.IsEnabled = false;
        try
        {
            var raw = await App.Services.Host.ReadObsLogAsync(latest.Path);
            if (string.IsNullOrEmpty(raw))
            {
                ToolStatus.Text = "日志读取失败。";
                return;
            }

            var analyzer = new ObsLogAnalyzer();
            var report = analyzer.Analyze(raw, latest.Name);
            App.Services.Diagnostic.LatestReport = report;

            AnalysisCard.IsVisible = true;
            AnalysisList.ItemsSource = report.Findings.Select(f => new FindingRow
            {
                Title = f.Occurrences > 1 ? $"{f.Title}（×{f.Occurrences}）" : f.Title,
                Detail = f.Suggestion
            }).ToList();

            ToolStatus.Text = report.HasIssues
                ? $"发现 {report.Findings.Count} 项问题（严重 {report.CriticalCount} / 错误 {report.ErrorCount} / 警告 {report.WarningCount}）。已写入诊断上下文。"
                : "日志未见明显异常。";
        }
        finally
        {
            AnalyzeBtn.IsEnabled = true;
        }
    }

    private sealed class FindingRow
    {
        public string Title { get; init; } = "";
        public string Detail { get; init; } = "";
    }

    private async void OnLocateConfig(object? sender, RoutedEventArgs e)
    {
        var loc = await App.Services.ObsConfig.LocateAsync();
        ToolStatus.Text = loc is null ? "定位失败。" :
            $"{loc.ConfigDir}（{(loc.Exists ? "存在" : "不存在")}）";
    }

    private async void OnBackup(object? sender, RoutedEventArgs e)
    {
        if (await App.Services.ObsConfig.IsRunningAsync())
        {
            ToolStatus.Text = "OBS 正在运行，建议先退出再备份，避免文件占用。仍将继续尝试…";
        }
        var path = await App.Services.ObsConfig.CreateBackupAsync("toolbox-manual");
        ToolStatus.Text = path is null ? "备份失败：未找到配置目录或磁盘不可写。" : $"已备份到：{path}";
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        var result = await App.Services.ObsConfig.ImportAsync("overwrite");
        ToolStatus.Text = result?.Message ?? (result?.Ok == true ? "导入完成。" : "导入失败。");
    }

    private async void OnReset(object? sender, RoutedEventArgs e)
    {
        // 二次确认：彻底重置会把整个 obs-studio 目录移走（先自动备份）
        var dialog = new Window
        {
            Title = "确认重置",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var confirm = new Button { Classes = { "primary" }, Content = "确认重置" };
        var cancel = new Button { Classes = { "secondary" }, Content = "取消" };
        confirm.Click += (_, _) => { dialog.Close(true); };
        cancel.Click += (_, _) => { dialog.Close(false); };
        dialog.Content = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Classes = { "body" },
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Text = "将把整个 OBS 配置目录移出原位（自动备份保留在应用数据目录），所有场景与设置恢复默认。确定继续？"
                },
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children = { cancel, confirm } }
            }
        };

        var owner = this.GetVisualRoot() as Window;
        var ok = owner is null ? false : await dialog.ShowDialog<bool?>(owner) == true;
        if (!ok) return;

        var result = await App.Services.ObsConfig.ResetFullAsync();
        ToolStatus.Text = result?.Message ?? "重置失败。";
    }
}
