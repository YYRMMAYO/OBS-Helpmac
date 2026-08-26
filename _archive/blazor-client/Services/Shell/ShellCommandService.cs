using OBS_Helper.Client.Services.Host;
using OBS_Helper.Client.Services.Obs;

namespace OBS_Helper.Client.Services.Shell;

/// <summary>
/// 桌面 Shell 动作分发（托盘菜单 / 全局热键 / 单实例唤起）。
///
/// Rust 宿主只是「事件触发器」：把动作字符串（toggleRecord / toggleStream /
/// toggleVCam / toggleMini / toggleMain / showMain / quit）推送到前端，
/// 由本服务分发到 ObsConnectionService 执行真正的 OBS 操作。
/// 同时订阅连接状态变化，把「录制中 / 推流中 / 虚拟摄像头」上报给宿主刷新托盘菜单。
/// </summary>
public sealed class ShellCommandService : IAsyncDisposable
{
    private readonly HostBridge _host;
    private readonly ObsConnectionService _conn;

    public ShellCommandService(HostBridge host, ObsConnectionService conn)
    {
        _host = host;
        _conn = conn;
        _host.ShellActionHandler = OnShellActionAsync;
        _conn.StateChanged += OnConnectionChanged;
    }

    /// <summary>应用启动时调用：开始监听 Tauri 事件并同步一次托盘状态。</summary>
    public async Task InitializeAsync()
    {
        await _host.StartShellListenerAsync();
        await ReportTrayStateAsync();
    }

    // ------------------------------------------------------------ 动作分发

    private async Task OnShellActionAsync(string action)
    {
        switch (action)
        {
            case "showMain":
                await _host.ShowMainWindowAsync();
                break;

            case "toggleMain":
                await _host.ToggleMainWindowAsync();
                break;

            case "toggleMini":
                await _host.ToggleMiniWindowAsync();
                break;

            case "toggleRecord":
                await ToggleRecordAsync();
                break;

            case "toggleStream":
                await ToggleStreamAsync();
                break;

            case "toggleVCam":
                await ToggleVCamAsync();
                break;

            case "quit":
                await _host.QuitAppAsync();
                break;
        }
    }

    private async Task ToggleRecordAsync()
    {
        if (!_conn.IsConnected) return;
        if (_conn.RecordStatus.Active)
            await _conn.StopRecordAsync();
        else
            await _conn.StartRecordAsync();
    }

    private async Task ToggleStreamAsync()
    {
        if (!_conn.IsConnected) return;
        if (_conn.StreamStatus.Active)
            await _conn.StopStreamAsync();
        else
            await _conn.StartStreamAsync();
    }

    private async Task ToggleVCamAsync()
    {
        if (!_conn.IsConnected) return;
        if (_conn.VirtualCamStatus.Active)
            await _conn.StopVirtualCamAsync();
        else
            await _conn.StartVirtualCamAsync();
    }

    // ------------------------------------------------------------ 托盘状态上报

    private void OnConnectionChanged() => _ = ReportTrayStateAsync();

    private async Task ReportTrayStateAsync()
    {
        if (!_host.IsAvailable) return;
        await _host.ReportTrayStateAsync(
            _conn.IsConnected,
            _conn.RecordStatus.Active,
            _conn.StreamStatus.Active,
            _conn.VirtualCamStatus.Active);
    }

    public async ValueTask DisposeAsync()
    {
        _host.ShellActionHandler = null;
        _conn.StateChanged -= OnConnectionChanged;
        await Task.CompletedTask;
    }
}
