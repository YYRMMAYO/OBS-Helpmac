using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OBS_Helper.MacOS.Views;

/// <summary>指引页（与 Windows 版 GuidePage 对齐）：官方链接 + macOS 专属贴士。</summary>
public partial class GuidePage : UserControl
{
    private sealed class LinkRow
    {
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string Url { get; init; } = "";
    }

    private static readonly LinkRow[] Links =
    {
        new()
        {
            Title = "OBS Studio 官方知识库",
            Description = "安装、性能优化与常见问题官方解答。",
            Url = "https://obsproject.com/kb"
        },
        new()
        {
            Title = "OBS Studio macOS 发布说明",
            Description = "各版本对 macOS 的支持变化与已知问题。",
            Url = "https://github.com/obsproject/obs-studio/releases"
        },
        new()
        {
            Title = "obs-websocket 官方文档",
            Description = "远程控制协议说明；本应用的实时控制基于该功能。",
            Url = "https://github.com/obsproject/obs-websocket/blob/main/docs/README.md"
        },
        new()
        {
            Title = "Apple：App 管理隐私权限",
            Description = "屏幕录制、麦克风、摄像头权限的管理位置。",
            Url = "https://support.apple.com/zh-cn/guide/mac-help/mchld5aa3599/mac"
        }
    };

    public GuidePage()
    {
        InitializeComponent();
        LinkList.ItemsSource = Links;
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string url)
            await App.Services.Host.OpenExternalAsync(url);
    }
}
