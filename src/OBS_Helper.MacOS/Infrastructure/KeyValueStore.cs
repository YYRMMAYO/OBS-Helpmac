using System.Text.Json;

namespace OBS_Helper.MacOS.Infrastructure;

/// <summary>
/// 桌面端键值存储：替代 Web 版的 localStorage。
/// 数据以 JSON 文件形式保存在用户应用数据目录（macOS: ~/Library/Application Support/OBS_Helper）。
/// 非机密项（界面偏好、OBS 地址等）存这里；机密项（密码、API Key）走 HostBridge 加密通道。
/// </summary>
public sealed class KeyValueStore
{
    private readonly string _filePath;
    private readonly object _gate = new();
    private Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public KeyValueStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OBS_Helper");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "store.json");
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            _map = new();
            AppLog.Error(ex, "store.json 读取/解析失败，已回退为空配置");
        }
    }

    public string? Get(string key)
    {
        lock (_gate)
        {
            return _map.TryGetValue(key, out var v) ? v : null;
        }
    }

    public void Set(string key, string value)
    {
        lock (_gate)
        {
            _map[key] = value;
            Persist();
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            if (_map.Remove(key)) Persist();
        }
    }

    private void Persist()
    {
        try
        {
            // 原子写入：先写临时文件再替换，避免进程中途崩溃留下半个 JSON 导致全部数据静默丢失
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_map));
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch
        {
            // 磁盘不可写时静默降级为内存态，与 Web 版无痕模式行为一致
            try { if (File.Exists(_filePath + ".tmp")) File.Delete(_filePath + ".tmp"); } catch { }
        }
    }
}
