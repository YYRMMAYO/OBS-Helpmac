using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OBS_Helper.MacOS.Views;

/// <summary>插件页（与 Windows 版 PluginsPage 对齐，目录与提示按 macOS 适配）。</summary>
public partial class PluginsPage : UserControl
{
    private sealed class PluginRow
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Status { get; init; } = "";
    }

    public PluginsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ScanAsync();
    }

    private Task ScanAsync()
    {
        var list = new List<PluginRow>();
        try
        {
            var dir = Services.Host.HostBridge.ObsConfigRoot is { } root
                ? System.IO.Path.Combine(root, "plugins")
                : "";
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (var d in Directory.EnumerateDirectories(dir).OrderBy(x => x))
                {
                    var hasContent = Directory.EnumerateFileSystemEntries(d).Any();
                    list.Add(new PluginRow
                    {
                        Name = System.IO.Path.GetFileName(d),
                        Path = d,
                        Status = hasContent ? "已安装" : "空目录"
                    });
                }
            }
            else
            {
                list.Add(new PluginRow { Name = "未找到插件目录", Path = dir, Status = "—" });
            }
        }
        catch (Exception ex)
        {
            list.Add(new PluginRow { Name = "扫描失败", Path = ex.Message, Status = "—" });
        }

        PluginList.ItemsSource = list;
        return Task.CompletedTask;
    }

    private async void OnScan(object? sender, RoutedEventArgs e) => await ScanAsync();

    private async void OnRevealPlugins(object? sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(Services.Host.HostBridge.ObsConfigRoot, "plugins");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        await App.Services.Host.RevealInFinderAsync(dir);
    }
}
