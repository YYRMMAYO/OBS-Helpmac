using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using OBS_Helper.Client.Services.Host;
using OBS_Helper.Client.Services.Obs;

namespace OBS_Helper.Client.Services.Shell;

/// <summary>一条场景自动切换规则（与 Windows 侧 AutoSwitchSettings.MatchRule 同构）。</summary>
public sealed class AutoSwitchRule
{
    public bool Enabled { get; set; } = true;

    /// <summary>匹配关键词或正则（匹配目标是当前前台应用的名称 / bundle id）。</summary>
    public string Pattern { get; set; } = "";

    /// <summary>true 时 Pattern 按正则解释，否则按「忽略大小写的包含」匹配。</summary>
    public bool UseRegex { get; set; }

    /// <summary>命中后切换到的 OBS 场景名。</summary>
    public string SceneName { get; set; } = "";

    public string Display => UseRegex ? $"/{Pattern}/ → {SceneName}" : $"\"{Pattern}\" → {SceneName}";
}

/// <summary>场景自动切换配置（存 localStorage，非敏感）。</summary>
public sealed class AutoSwitchSettings
{
    public bool Enabled { get; set; }
    public List<AutoSwitchRule> Rules { get; set; } = new();
}

/// <summary>
/// 场景自动切换（对应 Windows 侧 SceneAutoSwitcher）。
///
/// 平台差异：Windows 用 GetForegroundWindow 拿「前台窗口标题」，macOS 没有等价
/// 公开 API，宿主通过 lsappinfo 返回「前台应用名称 / bundle id」。因此规则匹配
/// 对象是<b>应用</b>而非窗口标题——覆盖绝大多数「切到某游戏/某软件就换场景」的
/// 场景。规则匹配逻辑与 Windows 侧一致：按列表顺序第一条命中生效、同一规则
/// 连续命中不重复发请求、规则切换之间至少间隔 3 秒、正则 250ms 超时防 ReDoS。
/// </summary>
public sealed class SceneAutoSwitcherService : IAsyncDisposable
{
    private const string StorageKey = "obshelper.autoswitch";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinSwitchGap = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly IJSRuntime _js;
    private readonly HostBridge _host;
    private readonly ObsConnectionService _conn;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private string _lastMatchedRule = "";

    public SceneAutoSwitcherService(IJSRuntime js, HostBridge host, ObsConnectionService conn)
    {
        _js = js;
        _host = host;
        _conn = conn;
    }

    public AutoSwitchSettings Settings { get; private set; } = new();

    /// <summary>启动 / 停止时触发，供设置页刷新开关状态。</summary>
    public event Action? Changed;

    public bool IsRunning => _loop is { IsCompleted: false };

    public async Task LoadAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(raw))
            {
                var s = JsonSerializer.Deserialize<AutoSwitchSettings>(raw);
                if (s is not null) Settings = Sanitize(s);
            }
        }
        catch (Exception)
        {
            // localStorage 不可用：使用默认（关闭）配置
        }
    }

    private static AutoSwitchSettings Sanitize(AutoSwitchSettings s)
    {
        s.Rules ??= new();
        s.Rules = s.Rules
            .Where(r => !string.IsNullOrWhiteSpace(r.Pattern) && !string.IsNullOrWhiteSpace(r.SceneName))
            .Take(50)
            .ToList();
        return s;
    }

    public async Task SaveAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(Settings));
        }
        catch (Exception)
        {
            // 静默降级
        }
    }

    /// <summary>启用 / 停用自动切换。</summary>
    public async Task SetEnabledAsync(bool enabled)
    {
        Settings.Enabled = enabled;
        await SaveAsync();
        if (enabled) Start();
        else await StopAsync();
        Changed?.Invoke();
    }

    public void Start()
    {
        if (IsRunning || !Settings.Enabled) return;
        _lastMatchedRule = "";
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
        Changed?.Invoke();
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
        Changed?.Invoke();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await SafeWaitAsync(timer, token))
        {
            if (!_conn.IsConnected) continue;
            var app = await _host.GetForegroundAppAsync();
            if (app is null) continue;

            // 构造匹配文本：应用名优先，同时包含 bundle id 便于精确匹配
            var haystack = string.IsNullOrEmpty(app.Name)
                ? app.BundleId
                : $"{app.Name} {app.BundleId}";

            var rule = MatchRule(haystack);
            if (rule is null)
            {
                _lastMatchedRule = "";
                continue;
            }

            // 同一规则连续命中不重复发请求
            var ruleKey = $"{rule.SceneName}|{rule.Pattern}";
            if (ruleKey == _lastMatchedRule) continue;
            _lastMatchedRule = ruleKey;

            if (_conn.CurrentScene != rule.SceneName)
            {
                await _conn.SetSceneAsync(rule.SceneName);
            }
            await Task.Delay(MinSwitchGap, token);
        }
    }

    private AutoSwitchRule? MatchRule(string haystack)
    {
        foreach (var r in Settings.Rules)
        {
            if (!r.Enabled || string.IsNullOrWhiteSpace(r.Pattern)) continue;
            bool hit;
            if (r.UseRegex)
            {
                try
                {
                    var re = new Regex(r.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
                    hit = re.IsMatch(haystack);
                }
                catch (Exception)
                {
                    // 正则编译失败 / 超时：静默跳过该规则
                    continue;
                }
            }
            else
            {
                hit = haystack.Contains(r.Pattern.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            if (hit) return r;
        }
        return null;
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
