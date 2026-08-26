using Avalonia.Controls;
using Avalonia.Interactivity;
using OBS_Helper.MacOS.Models.Obs;
using OBS_Helper.MacOS.Services.Obs;

namespace OBS_Helper.MacOS.Views;

/// <summary>控制台页（与 Windows 版 ConsolePage / MiniControlWindow 对齐）。</summary>
public partial class ConsolePage : UserControl
{
    private bool _recording, _streaming, _vcam;

    public ConsolePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadSettingsAsync();
        App.Services.Obs.StateChanged += OnStateChanged;
        UpdateState();
    }

    private async Task LoadSettingsAsync()
    {
        var s = App.Services.ObsSettings.Current;
        HostBox.Text = s.Host;
        PortBox.Text = s.Port.ToString();
        RememberPwd.IsChecked = s.RememberPassword;
        if (s.RememberPassword)
            PasswordBox.Text = await App.Services.ObsSettings.GetPasswordAsync() ?? "";
    }

    private void OnStateChanged()
        => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateState);

    private async void UpdateState()
    {
        var obs = App.Services.Obs;
        StateText.Text = obs.State switch
        {
            ObsConnectionState.Connected => "已连接",
            ObsConnectionState.Connecting => "连接中…",
            ObsConnectionState.Authenticating => "验证中…",
            ObsConnectionState.Reconnecting => "重连中…",
            ObsConnectionState.Failed => obs.LastError ?? "连接失败",
            _ => "未连接"
        };

        var connected = obs.IsConnected;
        StateDot.Fill = connected
            ? this.FindResource("SuccessBrush") as Avalonia.Media.IBrush ?? Avalonia.Media.Brushes.Green
            : this.FindResource("DangerBrush") as Avalonia.Media.IBrush ?? Avalonia.Media.Brushes.Gray;
        ConnectBtn.IsVisible = !connected;
        DisconnectBtn.IsVisible = connected;

        if (connected)
        {
            SceneList.ItemsSource = obs.Scenes.Select(sc => new SceneRow
            {
                Name = sc.Name,
                Label = sc.IsCurrent || sc.Name == obs.CurrentScene ? $"● {sc.Name}" : sc.Name
            }).ToList();
            UpdateOutputs();
        }
        else
        {
            SceneList.ItemsSource = new List<SceneRow>();
        }
    }

    private sealed class SceneRow
    {
        public string Name { get; init; } = "";
        public string Label { get; init; } = "";
    }

    private void UpdateOutputs()
    {
        var obs = App.Services.Obs;
        _recording = obs.RecordStatus.Active;
        _streaming = obs.StreamStatus.Active;
        _vcam = obs.VirtualCamStatus.Active;

        RecordBtn.Content = _recording ? "停止录制" : "开始录制";
        StreamBtn.Content = _streaming ? "停止推流" : "开始推流";
        VcamBtn.Content = _vcam ? "关闭虚拟摄像头" : "启动虚拟摄像头";

        OutputStatus.Text =
            $"录制：{(_recording ? obs.RecordStatus.Timecode : "未在录制")}　" +
            $"推流：{(_streaming ? obs.StreamStatus.Timecode + $"（拥塞 {obs.StreamStatus.Congestion:P0}）" : "未在推流")}　" +
            $"虚拟摄像头：{(_vcam ? "运行中" : "已停止")}";
    }

    private async void OnConnect(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        var settings = new ObsConnectionSettings
        {
            Host = string.IsNullOrWhiteSpace(HostBox.Text) ? "127.0.0.1" : HostBox.Text.Trim(),
            Port = int.TryParse(PortBox.Text, out var p) && p is >= 1 and <= 65535 ? p : 4455,
            AutoConnect = App.Services.ObsSettings.Current.AutoConnect,
            AutoReconnect = true,
            RememberPassword = RememberPwd.IsChecked == true
        };
        await App.Services.ObsSettings.SaveAsync(settings);

        ConnectBtn.IsEnabled = false;
        try
        {
            var pwd = string.IsNullOrEmpty(PasswordBox.Text) ? null : PasswordBox.Text;
            var ok = await App.Services.Obs.ConnectAsync(pwd);
            await App.Services.ObsSettings.SetPasswordAsync(
                ok ? PasswordBox.Text : null, RememberPwd.IsChecked == true);
            if (!ok)
            {
                ErrorText.Text = App.Services.Obs.LastError ?? "无法连接到 OBS。请确认 OBS 已启动，并在「设置 → WebSocket 服务器」开启远程访问。";
                ErrorText.IsVisible = true;
            }
        }
        finally
        {
            ConnectBtn.IsEnabled = true;
        }
    }

    private async void OnDisconnect(object? sender, RoutedEventArgs e)
        => await App.Services.Obs.DisconnectAsync();

    private async void OnSwitchScene(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string name)
            await App.Services.Obs.SetSceneAsync(name);
    }

    private async void OnRecord(object? sender, RoutedEventArgs e)
        => await SafeSend(_recording
            ? () => App.Services.Obs.StopRecordAsync()
            : () => App.Services.Obs.StartRecordAsync());

    private async void OnStream(object? sender, RoutedEventArgs e)
        => await SafeSend(_streaming
            ? () => App.Services.Obs.StopStreamAsync()
            : () => App.Services.Obs.StartStreamAsync());

    private async void OnVcam(object? sender, RoutedEventArgs e)
        => await SafeSend(_vcam
            ? () => App.Services.Obs.StopVirtualCamAsync()
            : () => App.Services.Obs.StartVirtualCamAsync());

    private async Task SafeSend(Func<Task<ObsRequestResult>> action)
    {
        try
        {
            await action();
            await App.Services.Obs.RefreshOutputsAsync();
        }
        catch { /* 输出状态会在下一次刷新时呈现 */ }
        UpdateOutputs();
    }
}
