using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using OBS_Helper.MacOS.Services.Obs;
using OBS_Helper.MacOS.Services.Ai;

namespace OBS_Helper.MacOS.Views;

/// <summary>设置页（与 Windows 版 SettingsPage 对齐）：外观 / 连接 / AI / 关于。</summary>
public partial class SettingsPage : UserControl
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
        // 只订阅一次：页面被缓存而 Loaded 可多次触发，重复订阅会导致保存动作被执行多次。
        // 用 IsCheckedChanged 单一事件（Checked/Unchecked 已过时），加载期间挂 _loading 防止回写。
        AutoConnect.IsCheckedChanged += OnConnectionChanged;
        RememberPassword.IsCheckedChanged += OnConnectionChanged;
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var svc = App.Services;

            var dark = (Application.Current?.RequestedThemeVariant ?? ThemeVariant.Light) == ThemeVariant.Dark;
            ThemeDark.IsChecked = dark;
            ThemeLight.IsChecked = !dark;

            AutoConnect.IsChecked = svc.ObsSettings.Current.AutoConnect;
            RememberPassword.IsChecked = svc.ObsSettings.Current.RememberPassword;

            await svc.AiSettings.LoadAsync();
            var cloud = svc.AiSettings.Mode == DiagnosticEngineMode.Cloud;
            EngineCloud.IsChecked = cloud;
            EngineLocal.IsChecked = !cloud;
            CloudUrlBox.Text = svc.AiSettings.Settings.CloudUrl;
            CloudModelBox.Text = svc.AiSettings.Settings.CloudModel;
            RefreshAiStatus();

            AboutText.Text =
                $"OBS 排障助手 for macOS v{typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)}\n" +
                $"平台：{svc.Host.Platform} · 问题库：{(await svc.Problems.GetProblemsAsync()).Count} 条 · 全部诊断在本地完成。";
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnThemeChanged(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is null) return;
        var dark = ThemeDark.IsChecked == true;
        Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        App.Services.Store.Set("ui.theme", dark ? "dark" : "light");
    }

    private async void OnConnectionChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return; // 页面加载时的程序化赋值不触发保存
        var cur = App.Services.ObsSettings.Current;
        await App.Services.ObsSettings.SaveAsync(new ObsConnectionSettings
        {
            Host = cur.Host,
            Port = cur.Port,
            AutoConnect = AutoConnect.IsChecked == true,
            AutoReconnect = cur.AutoReconnect,
            RememberPassword = RememberPassword.IsChecked == true
        });
    }

    private async void OnSaveAi(object? sender, RoutedEventArgs e)
    {
        SaveAiBtn.IsEnabled = false;
        try
        {
            var keyName = App.Services.AiSettings.Settings.CloudSecretKeyName;
            var key = CloudKeyBox.Text?.Trim();
            if (!string.IsNullOrEmpty(key))
                await App.Services.Host.SetSecretAsync(keyName, key);

            await App.Services.AiSettings.SetCloudAsync(
                CloudUrlBox.Text ?? "", keyName, CloudModelBox.Text ?? "");

            if (EngineCloud.IsChecked == true)
                await App.Services.AiSettings.SetModeAsync(DiagnosticEngineMode.Cloud);
            else
                await App.Services.AiSettings.SetModeAsync(DiagnosticEngineMode.Local);

            RefreshAiStatus();
            AiStatusText.Text = "已保存。" + AiStatusText.Text;
        }
        finally
        {
            SaveAiBtn.IsEnabled = true;
        }
    }

    private void RefreshAiStatus()
    {
        var ai = App.Services.AiSettings;
        AiStatusText.Text = ai.IsCloudConfigured
            ? "云端引擎已就绪（API Key 保存在系统钥匙串，不会明文落盘）。"
            : "当前使用本地离线引擎；如需云端大模型，填写 https 接口地址并保存 API Key。";
    }

    private async void OnCheckUpdate(object? sender, RoutedEventArgs e)
    {
        UpdateText.Text = "正在检查更新…";
        try
        {
            var tags = await App.Services.Host.CheckAppUpdateAsync();
            UpdateText.Text = tags is null or { Count: 0 }
                ? "暂时无法获取版本信息（可能处于离线状态）。"
                : $"最新版本：{tags[0]}　当前：v{typeof(SettingsPage).Assembly.GetName().Version?.ToString(3)}";
        }
        catch (Exception ex)
        {
            UpdateText.Text = $"检查失败：{ex.Message}";
        }
    }

    private async void OnOpenRepo(object? sender, RoutedEventArgs e)
        => await App.Services.Host.OpenExternalAsync("https://github.com/YYRMMAYO/OBS-Helpmac");
}
