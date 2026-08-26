using Avalonia.Controls;
using Avalonia.Interactivity;
using OBS_Helper.MacOS.Infrastructure;

namespace OBS_Helper.MacOS.Views;

/// <summary>搭建页（与 Windows 版 SetupPage 对齐，步骤按 macOS 适配）。</summary>
public partial class SetupPage : UserControl
{
    private sealed class StepRow
    {
        public int Index { get; init; }
        public string Title { get; init; } = "";
        public string Detail { get; init; } = "";
        public bool Done { get; set; }
    }

    private static readonly (string Title, string Detail)[] DefaultSteps =
    {
        ("安装 OBS Studio", "从 obsproject.com 下载 macOS 版（Apple Silicon 选 aarch64 安装包），拖入「应用程序」完成安装。"),
        ("授予屏幕录制权限", "系统设置 → 隐私与安全性 → 屏幕录制，勾选 OBS 后重启 OBS。否则采集窗口 / 显示器会黑屏。"),
        ("授予麦克风权限", "系统设置 → 隐私与安全性 → 麦克风，勾选 OBS；首次弹出授权时选择「允许」。"),
        ("创建直播场景", "场景集合建议按用途分开：游戏、摄像头、会议分享。可用「模板」页一键生成常用布局。"),
        ("配置视频参数", "设置 → 视频：基础分辨率 1920×1080，输出分辨率 1920×1080，帧率 60（M 系芯片可放心开 60）。"),
        ("配置编码器", "M1/M2/M3 芯片使用 Apple Silicon 硬件编码（VideoToolbox）；码率：B站 8000kbps、抖音 6000kbps 起步。"),
        ("连接直播平台", "设置 → 推流：选择服务或自定义 RTMP 地址。B 站 / 抖音 / 视频号可在平台开播页获取推流码。"),
        ("开启 WebSocket 远程控制", "工具 → WebSocket 服务器设置：开启服务并记住端口与密码，然后在「控制台」页连接。"),
        ("试播验证", "用低码率做一次私密试播，观察「监控」页的丢帧与帧渲染耗时，确认稳定后再正式开播。")
    };

    private const string StoreKey = "setup.steps.done";

    public SetupPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadSteps();
    }

    private void LoadSteps()
    {
        var raw = App.Services.Store.Get(StoreKey);
        var doneSet = new HashSet<int>();
        if (!string.IsNullOrEmpty(raw))
            foreach (var s in raw.Split(',')) if (int.TryParse(s, out var i)) doneSet.Add(i);

        StepList.ItemsSource = DefaultSteps.Select((t, i) => new StepRow
        {
            Index = i,
            Title = $"{i + 1}. {t.Title}",
            Detail = t.Detail,
            Done = doneSet.Contains(i)
        }).ToList();
    }

    private void OnStepChanged(object? sender, RoutedEventArgs e)
    {
        if (StepList.ItemsSource is not List<StepRow> rows) return;
        var done = rows.Where(r => r.Done).Select(r => r.Index);
        App.Services.Store.Set(StoreKey, string.Join(",", done));
    }
}
