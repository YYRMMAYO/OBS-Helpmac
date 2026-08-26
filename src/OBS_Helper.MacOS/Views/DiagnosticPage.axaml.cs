using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OBS_Helper.MacOS.Services.Ai;

namespace OBS_Helper.MacOS.Views;

/// <summary>诊断页（与 Windows 版 DiagnosticPage 对齐）：环境扫描 + 引擎归因。</summary>
public partial class DiagnosticPage : UserControl
{
    private sealed class RowViewModel
    {
        public string Title { get; init; } = "";
        public string Detail { get; init; } = "";
        public string Suggestion { get; init; } = "";
        public IBrush DotBrush { get; init; } = Brushes.Gray;
    }

    private static readonly Dictionary<DiagnosticSeverity, string> SeverityNames = new()
    {
        [DiagnosticSeverity.Critical] = "严重",
        [DiagnosticSeverity.Error] = "错误",
        [DiagnosticSeverity.Warning] = "警告",
        [DiagnosticSeverity.Suggestion] = "建议",
        [DiagnosticSeverity.Info] = "提示"
    };

    public DiagnosticPage()
    {
        InitializeComponent();
    }

    private async void OnDiagnose(object? sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Progress.IsVisible = true;
        ResultList.ItemsSource = new List<RowViewModel>();
        SummaryCard.IsVisible = false;

        try
        {
            var orch = App.Services.Diagnostic;
            var allowNetwork = AllowNetwork.IsChecked == true;
            await orch.ScanEnvironmentAsync(allowNetwork);
            var result = await orch.DiagnoseAsync();

            if (!result.Success)
            {
                SummaryCard.IsVisible = true;
                SummaryText.Text = $"诊断未完成：{result.Error}";
                return;
            }

            ResultList.ItemsSource = result.Items.Select(i => new RowViewModel
            {
                Title = $"[{SeverityNames.GetValueOrDefault(i.Severity, "提示")}] {i.Title}",
                Detail = string.IsNullOrEmpty(i.Reason) ? i.Source : $"{i.Source} · {i.Reason}",
                Suggestion = i.Steps.Count > 0 ? string.Join("\n", i.Steps.Select((s, idx) => $"{idx + 1}. {s}")) : i.Evidence,
                DotBrush = i.Severity switch
                {
                    DiagnosticSeverity.Critical or DiagnosticSeverity.Error =>
                        (IBrush?)this.FindResource("DangerBrush") ?? Brushes.Red,
                    DiagnosticSeverity.Warning => (IBrush?)this.FindResource("WarningBrush") ?? Brushes.Orange,
                    _ => (IBrush?)this.FindResource("AccentBrush") ?? Brushes.DodgerBlue
                }
            }).ToList();

            SummaryCard.IsVisible = !string.IsNullOrEmpty(result.Summary);
            SummaryText.Text = result.Summary + (result.FellBackToLocal ? "\n（云端引擎不可用，已自动回退本地引擎）" : "");
        }
        catch (Exception ex)
        {
            SummaryCard.IsVisible = true;
            SummaryText.Text = $"诊断异常：{ex.Message}";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            Progress.IsVisible = false;
        }
    }
}
