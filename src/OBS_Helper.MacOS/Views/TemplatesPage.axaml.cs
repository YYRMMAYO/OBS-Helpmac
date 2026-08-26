using Avalonia.Controls;
using Avalonia.Interactivity;
using OBS_Helper.MacOS.Models.ObsConfig;

namespace OBS_Helper.MacOS.Views;

/// <summary>模板页（与 Windows 版 TemplatePage 对齐）：在线落地 / 离线导出。</summary>
public partial class TemplatesPage : UserControl
{
    private sealed class RowViewModel
    {
        public SceneTemplate Template { get; init; } = new();
        public string Id => Template.Id;
        public string Name => Template.Title;
        public string Description => Template.Summary;
        public string Meta { get; init; } = "";
    }

    public TemplatesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var tpls = await App.Services.Templates.LoadAsync();
            TemplateList.ItemsSource = tpls.Select(t => new RowViewModel
            {
                Template = t,
                Meta = $"{t.Scenes.Count} 个场景"
            }).ToList();

            HintText.Text = App.Services.Obs.IsConnected
                ? "已连接 OBS：可一键在线落地（会自动先备份当前配置）。"
                : "未连接 OBS：可导出为场景集合 JSON，再在 OBS「场景集合 → 导入」中使用。";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"模板加载失败：{ex.Message}";
        }
    }

    private async void OnApply(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        Progress.IsVisible = true;
        StatusText.Text = "正在落地…";
        try
        {
            var result = await App.Services.Templates.ApplyAsync(id, true,
                msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusText.Text = msg));
            StatusText.Text = result.Ok
                ? "落地完成！请回到 OBS 检查场景与来源。" : $"失败：{result.Error}";
        }
        finally
        {
            Progress.IsVisible = false;
        }
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        var path = await App.Services.Templates.ExportAsync(id, null);
        StatusText.Text = path is null ? "导出取消或失败。" : $"已导出到：{path}";
    }
}
