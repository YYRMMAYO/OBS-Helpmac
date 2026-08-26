using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OBS_Helper.MacOS.Models.Obs;
using OBS_Helper.MacOS.Services.Obs;

namespace OBS_Helper.MacOS.Views;

public partial class MainWindow : Window
{
    private HomePage? _home;
    private SearchPage? _search;
    private AssistantPage? _assistant;
    private DiagnosticPage? _diagnostic;
    private SetupPage? _setup;
    private TemplatesPage? _templates;
    private PluginsPage? _plugins;
    private ToolboxPage? _toolbox;
    private ConsolePage? _console;
    private MonitorPage? _monitor;
    private GuidePage? _guide;
    private SettingsPage? _settings;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"} · macOS";

        App.Services.Obs.StateChanged += OnStateChanged;
        UpdateConnectionBadge();

        Closed += (_, _) =>
        {
            App.Services.Obs.StateChanged -= OnStateChanged;
            _ = App.Services.Monitor.StopAsync();
        };
    }

    private void OnNavChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || !rb.IsChecked.HasValue) return;

        (Control Page, string Title, string Subtitle) target = rb.Name switch
        {
            "NavHome"       => ((_home ??= new HomePage()), "首页", "按分类查问题，或直接问助手"),
            "NavSearch"     => ((_search ??= new SearchPage()), "搜索", "全文检索 212+ 条排障方案"),
            "NavAssistant"  => ((_assistant ??= new AssistantPage()), "AI 助手", "本地离线引擎，可选云端大模型"),
            "NavDiagnostic" => ((_diagnostic ??= new DiagnosticPage()), "诊断", "日志分析 + 环境体检 + AI 归因"),
            "NavSetup"      => ((_setup ??= new SetupPage()), "搭建", "从零到开播的分步指引"),
            "NavTemplates"  => ((_templates ??= new TemplatesPage()), "场景模板", "一键落地常用直播间布局"),
            "NavPlugins"    => ((_plugins ??= new PluginsPage()), "插件", "macOS 插件目录与管理"),
            "NavToolbox"    => ((_toolbox ??= new ToolboxPage()), "工具箱", "日志 / 配置 / 备份快捷操作"),
            "NavConsole"    => ((_console ??= new ConsolePage()), "控制台", "连接 OBS 并实时控制"),
            "NavMonitor"    => ((_monitor ??= new MonitorPage()), "监控", "帧率、丢帧与磁盘健康"),
            "NavGuide"      => ((_guide ??= new GuidePage()), "指引", "官方文档与常见指引"),
            "NavSettings"   => ((_settings ??= new SettingsPage()), "设置", "外观、连接与 AI 引擎"),
            _ => ((Control)PageHost.Content!, "首页", "")
        };

        PageHost.Content = target.Page;
        PageTitle.Text = target.Title;
        PageSubtitle.Text = target.Subtitle;
    }

    /// <summary>程序化导航（首页快捷入口等）。</summary>
    public void NavigateTo(string navName)
    {
        var rb = this.FindControl<RadioButton>(navName);
        if (rb is null || rb.IsChecked == true) return;
        rb.IsChecked = true;
    }

    /// <summary>打开问题库（可带分类过滤或直接定位某个问题）。</summary>
    public void OpenProblem(string? categoryId = null, string? problemId = null)
    {
        var page = _search ??= new SearchPage();
        if (categoryId is not null) page.FilterCategory(categoryId);
        else if (problemId is not null) page.ShowProblem(problemId);
        NavigateTo("NavSearch");
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        NavSettings.IsChecked = true;
        if (PageHost.Content != _settings) OnNavChecked(NavSettings, new RoutedEventArgs());
    }

    private void OnStateChanged()
        { Avalonia.Threading.Dispatcher.UIThread.Post(UpdateConnectionBadge); }

    private void UpdateConnectionBadge()
    {
        var obs = App.Services.Obs;
        var (text, color) = obs.State switch
        {
            ObsConnectionState.Connected => ("已连接 OBS", (IBrush?)this.FindResource("SuccessBrush")),
            ObsConnectionState.Connecting => ("连接中…", (IBrush?)this.FindResource("WarningBrush")),
            ObsConnectionState.Authenticating => ("验证中…", (IBrush?)this.FindResource("WarningBrush")),
            ObsConnectionState.Reconnecting => ("重连中…", (IBrush?)this.FindResource("WarningBrush")),
            ObsConnectionState.Failed => ("连接失败", (IBrush?)this.FindResource("DangerBrush")),
            _ => ("未连接 OBS", (IBrush?)this.FindResource("TextSecondaryBrush"))
        };
        SideState.Text = text;
        SideDot.Fill = color ?? Brushes.Gray;
    }
}
