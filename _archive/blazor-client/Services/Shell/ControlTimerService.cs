using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using OBS_Helper.Client.Services.Obs;

namespace OBS_Helper.Client.Services.Shell;

/// <summary>定时停止任务（录制 / 推流倒计时，对应 Windows 侧 ControlTimerService）。</summary>
public sealed class ControlTimerItem
{
    /// <summary>"record" | "stream"</summary>
    public string Kind { get; set; } = "";
    public string KindText => Kind == "record" ? "录制" : "推流";
    /// <summary>目标结束时刻（本地时间）。</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>输出最近一次处于活跃状态的时刻（用于「被手动停止」检测，不持久化）。</summary>
    [JsonIgnore]
    public DateTime LastActiveAt { get; set; } = DateTime.Now;

    public int RemainingSeconds => EndsAt is { } e ? Math.Max(0, (int)(e - DateTime.Now).TotalSeconds) : 0;

    public string RemainingText => RemainingSeconds switch
    {
        0 => "到点",
        < 60 => $"{RemainingSeconds} 秒",
        _ => $"{RemainingSeconds / 60} 分 {RemainingSeconds % 60} 秒"
    };
}

/// <summary>
/// 定时停止录制 / 推流（对应 Windows 侧 ControlTimerService）。
///
/// 给录制 / 推流设置倒计时，到点自动停止并触发界面提示；目标被手动停止
/// （10 秒宽限期）后自动取消对应定时。前端实现（Blazor Timer 轮询），不依赖桌面壳。
/// </summary>
public sealed class ControlTimerService : IAsyncDisposable
{
    private const string StorageKey = "obshelper.timers";
    private const int GraceSeconds = 10;

    private readonly IJSRuntime _js;
    private readonly ObsConnectionService _conn;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ControlTimerService(IJSRuntime js, ObsConnectionService conn)
    {
        _js = js;
        _conn = conn;
    }

    public List<ControlTimerItem> Items { get; private set; } = new();

    public event Action? Changed;

    public bool IsRunning => _loop is { IsCompleted: false };

    public async Task LoadAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(raw))
            {
                var list = JsonSerializer.Deserialize<List<ControlTimerItem>>(raw);
                if (list is not null)
                {
                    // 只保留还没到点的
                    Items = list.Where(i => i.EndsAt is { } e && e > DateTime.Now).ToList();
                }
            }
        }
        catch (Exception)
        {
            // localStorage 不可用
        }
        if (Items.Count > 0) Start();
    }

    private async Task SaveAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(Items));
        }
        catch (Exception)
        {
            // 静默降级
        }
    }

    /// <summary>给录制 / 推流设定倒计时（分钟，1~300）。</summary>
    public async Task<bool> StartTimerAsync(string kind, int minutes)
    {
        if (minutes is < 1 or > 300) return false;
        var item = new ControlTimerItem
        {
            Kind = kind,
            EndsAt = DateTime.Now.AddMinutes(minutes),
            LastActiveAt = DateTime.Now
        };
        Items.RemoveAll(i => i.Kind == kind);
        Items.Add(item);
        await SaveAsync();
        Start();
        Changed?.Invoke();
        return true;
    }

    public async Task CancelAsync(string kind)
    {
        Items.RemoveAll(i => i.Kind == kind);
        await SaveAsync();
        if (Items.Count == 0) await StopAsync();
        Changed?.Invoke();
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        var cts = _cts;
        var loop = _loop;
        _cts = null;
        _loop = null;
        if (cts is null) return;
        cts.Cancel();
        try { if (loop is not null) await loop; } catch (OperationCanceledException) { }
        cts.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await SafeWaitAsync(timer, token))
        {
            var now = DateTime.Now;
            bool dirty = false;

            foreach (var item in Items.ToList())
            {
                if (item.EndsAt is not { } endsAt) continue;

                // 输出状态检测：活跃则刷新 LastActiveAt；非活跃超过宽限期 = 用户手动停止了
                bool active = item.Kind == "record"
                    ? _conn.RecordStatus.Active || _conn.RecordStatus.Paused
                    : _conn.StreamStatus.Active;
                if (active)
                {
                    item.LastActiveAt = now;
                }
                else if ((now - item.LastActiveAt).TotalSeconds > GraceSeconds)
                {
                    // 用户手动停止（或从未真正开始）：自动取消定时，避免「幽灵倒计时」
                    Items.Remove(item);
                    dirty = true;
                    continue;
                }

                if (now >= endsAt)
                {
                    if (item.Kind == "record") await _conn.StopRecordAsync();
                    else await _conn.StopStreamAsync();
                    Items.Remove(item);
                    dirty = true;
                }
            }

            if (dirty)
            {
                await SaveAsync();
                if (Items.Count == 0) await StopAsync();
            }
            Changed?.Invoke();
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await Task.CompletedTask;
    }
}
