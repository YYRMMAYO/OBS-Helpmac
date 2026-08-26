using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OBS_Helper.MacOS.Services.Host;

/// <summary>宿主上报的运行环境信息。</summary>
public sealed class HostEnvironment
{
    public string Platform { get; set; } = "none";
    public string AppVersion { get; set; } = "";
    /// <summary>本机 OBS 日志目录（宿主解析，前端只读展示）。</summary>
    public string ObsLogDirectory { get; set; } = "";
    public bool LogDirectoryExists { get; set; }
}

/// <summary>OBS 日志文件条目。</summary>
public sealed class HostLogFile
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public long Size { get; set; }

    /// <summary>最后修改时间（Unix 毫秒）。统一用时间戳上报，避免时区/格式差异。</summary>
    public long Modified { get; set; }

    public DateTime ModifiedLocal => Modified <= 0
        ? DateTime.MinValue
        : DateTimeOffset.FromUnixTimeMilliseconds(Modified).LocalDateTime;

    public string ModifiedText => Modified <= 0 ? "—" : ModifiedLocal.ToString("yyyy-MM-dd HH:mm");

    public string SizeText => Size >= 1024 * 1024
        ? $"{Size / 1024.0 / 1024.0:0.0} MB"
        : $"{Size / 1024.0:0.0} KB";
}

/// <summary>OBS 配置目录下的一个条目（文件或子目录）。</summary>
public sealed class HostConfigEntry
{
    public string Name { get; set; } = "";
    public bool IsDir { get; set; }
    public long Size { get; set; }

    /// <summary>最后修改时间（Unix 毫秒）。</summary>
    public long Modified { get; set; }

    public DateTime ModifiedLocal => Modified <= 0
        ? DateTime.MinValue
        : DateTimeOffset.FromUnixTimeMilliseconds(Modified).LocalDateTime;
}

/// <summary>桌面壳偏好（对应 Windows 侧 ShellSettings 的桌面部分）。</summary>
public sealed class HostShellPrefs
{
    /// <summary>关闭主窗口时是否最小化到托盘（而非退出）。</summary>
    public bool CloseToTray { get; set; }
}

/// <summary>OBS 配置目录定位结果。</summary>
public sealed class HostConfigLocation
{
    public string ConfigDir { get; set; } = "";
    public bool Exists { get; set; }
    public bool Portable { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>备份目录里的一条备份记录。</summary>
public sealed class HostBackupInfo
{
    public string Path { get; set; } = "";
    /// <summary>创建时间（Unix 毫秒）。</summary>
    public long CreatedAt { get; set; }
    public string Reason { get; set; } = "";
    public bool IncludeKey { get; set; }
    public bool IncludePluginConfig { get; set; }

    public DateTime CreatedAtLocal => CreatedAt <= 0
        ? DateTime.MinValue
        : DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).LocalDateTime;

    public string CreatedAtText => CreatedAtLocal == DateTime.MinValue ? "—" : CreatedAtLocal.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>配置导入结果。</summary>
public sealed class HostImportResult
{
    public bool Ok { get; set; }
    public int ImportedCollections { get; set; }
    public int ImportedProfiles { get; set; }
    public string? AutoBackupPath { get; set; }
    public string? Message { get; set; }
}

/// <summary>彻底重置结果。</summary>
public sealed class HostResetResult
{
    public bool Ok { get; set; }
    public string? AutoBackupPath { get; set; }
    public string? TrashPath { get; set; }
    public string? Message { get; set; }
}

/// <summary>一次系统资源采样。</summary>
public sealed class HostSystemSample
{
    public double CpuPercent { get; set; }
    public double MemUsedMb { get; set; }
    public double MemTotalMb { get; set; }
    public double MemUsedPercent { get; set; }
    public double NetDownKbps { get; set; }
    public double NetUpKbps { get; set; }
    public List<HostDiskSample> Disks { get; set; } = new();

    /// <summary>剩余空间最小的一块盘（用于磁盘预警）。</summary>
    public HostDiskSample? LowestDisk => Disks.Count == 0 ? null : Disks.OrderBy(d => d.FreeGb).First();
}

/// <summary>磁盘采样。</summary>
public sealed class HostDiskSample
{
    public string Name { get; set; } = "";
    public double TotalGb { get; set; }
    public double FreeGb { get; set; }
    public double FreePercent => TotalGb > 0 ? FreeGb / TotalGb * 100.0 : 0;
}

/// <summary>
/// 桌面宿主能力的原生实现（Avalonia 版）。
/// 与 Web 版语义一致：机密（obs-websocket 密码、LLM API Key）不进明文存储 ——
/// macOS 走系统钥匙串（security CLI，密文经 stdin 传递）；其它平台显式拒绝，绝不静默降级为明文。
/// </summary>
public sealed class HostBridge
{
    private readonly Infrastructure.KeyValueStore _store;
    private static readonly HttpClient Http = CreateHttp();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HostBridge(Infrastructure.KeyValueStore store) => _store = store;

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("OBS-Helper-MacOS/2.0");
        c.Timeout = TimeSpan.FromSeconds(30);
        return c;
    }

    public bool IsAvailable => true;

    public string Platform =>
        OperatingSystem.IsMacOS() ? "macos" :
        OperatingSystem.IsWindows() ? "windows" : "linux";

    /// <summary>探测宿主是否存在。桌面版恒为 true。</summary>
    public Task<bool> ProbeAsync() => Task.FromResult(true);

    // ------------------------------------------------------------ 应用数据目录

    private static string AppDataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OBS_Helper");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string BackupDir => EnsureDir(Path.Combine(AppDataDir, "backups"));
    private static string TrashDir => EnsureDir(Path.Combine(AppDataDir, "trash"));

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 判断 full 是否严格位于 root 目录内部。
    /// 用 GetRelativePath 而非 StartsWith 前缀匹配：后者会被同级目录名绕过
    /// （如 root=obs-studio 时 obs-studio-backup 也能通过校验）。
    /// </summary>
    private static bool IsInsideRoot(string root, string full)
    {
        var rel = Path.GetRelativePath(root, full);
        return rel != "." && !rel.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(rel);
    }

    // ------------------------------------------------------------ 进程辅助

    /// <summary>
    /// 运行外部命令。stdout / stderr 始终被并发排空（避免管道缓冲区写满导致子进程死锁），
    /// 超时后强制结束子进程。
    /// </summary>
    private static async Task<(int Code, string Stdout, string Stderr)> RunAsync(
        string fileName, IReadOnlyList<string> args, string? stdin = null, int timeoutMs = 8000)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin is not null,
                CreateNoWindow = true
            };
            foreach (var a in args) p.StartInfo.ArgumentList.Add(a);

            p.Start();

            // 先启动两个读取任务再等待退出，保证管道被持续排空
            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();

            if (stdin is not null)
            {
                await p.StandardInput.WriteAsync(stdin);
                p.StandardInput.Close();
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await p.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch { }
            }

            await Task.WhenAll(soTask, seTask);
            return (p.ExitCode, soTask.Result, seTask.Result);
        }
        catch
        {
            return (-1, "", "");
        }
    }

    private static (int Code, string Stdout) Run(string fileName, string args, int timeoutMs = 8000)
    {
        var (code, so, _) = RunAsync(fileName, SplitArgs(args), timeoutMs: timeoutMs)
            .GetAwaiter().GetResult();
        return (code, so);
    }

    /// <summary>极简参数切分：仅用于内部写死的参数串（如 lsappinfo info -only bundleid front）。</summary>
    private static string[] SplitArgs(string args)
        => string.IsNullOrWhiteSpace(args) ? Array.Empty<string>() : args.Split(' ');

    private static void OpenInBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) Process.Start(new ProcessStartInfo("open", $"\"{url}\"") { UseShellExecute = false });
            else if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo("xdg-open", $"\"{url}\"") { UseShellExecute = false });
        }
        catch { }
    }

    // ------------------------------------------------------------ 机密存储

    private const string KeychainService = "OBS_Helper";

    /// <summary>
    /// 写入一条机密到 macOS 系统钥匙串。
    /// 密文经 <b>stdin</b> 传给 <c>security</c>，绝不进入命令行参数（argv 会被本机任意进程读到），
    /// 参数一律用 ArgumentList 传递以杜绝注入。
    /// 非 macOS 平台显式拒绝：不做明文落盘降级。
    /// </summary>
    public async Task<bool> SetSecretAsync(string key, string value)
    {
        if (!OperatingSystem.IsMacOS()) return false;
        try
        {
            var (code, _, _) = await RunAsync("/usr/bin/security",
                new[] { "add-generic-password", "-U", "-s", KeychainService, "-a", key, "-w" },
                stdin: value);
            return code == 0;
        }
        catch (Exception ex)
        {
            Infrastructure.AppLog.Error(ex, "钥匙串写入失败：" + key);
            return false;
        }
    }

    /// <summary>读取一条机密；不存在时返回 null。</summary>
    public async Task<string?> GetSecretAsync(string key)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        try
        {
            var (code, so, _) = await RunAsync("/usr/bin/security",
                new[] { "find-generic-password", "-s", KeychainService, "-a", key, "-w" });
            if (code != 0 || so.Length == 0)
            {
                if (code != 44) Infrastructure.AppLog.Warn($"钥匙串读取失败（exit {code}）：" + key);
                return null;
            }
            // security 输出末尾固定带一个换行；只剥掉这一个，保留密码本身的边界字符
            return so.EndsWith('\n') ? so[..^1] : so;
        }
        catch (Exception ex)
        {
            Infrastructure.AppLog.Error(ex, "钥匙串读取异常：" + key);
            return null;
        }
    }

    /// <summary>删除一条机密。</summary>
    public async Task<bool> DeleteSecretAsync(string key)
    {
        if (OperatingSystem.IsMacOS())
        {
            await RunAsync("/usr/bin/security",
                new[] { "delete-generic-password", "-s", KeychainService, "-a", key });
        }
        _store.Remove("secret:" + key);
        return true;
    }

    // ------------------------------------------------------------ 目录定位

    /// <summary>OBS 配置根目录（跨平台）。</summary>
    public static string ObsConfigRoot
    {
        get
        {
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "obs-studio");
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "obs-studio");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "obs-studio");
        }
    }

    public static string ObsLogDirectory => Path.Combine(ObsConfigRoot, "logs");

    // ------------------------------------------------------------ 日志访问

    /// <summary>列出本机 OBS 日志目录中的日志文件（按修改时间倒序，最多 20 条）。</summary>
    public Task<List<HostLogFile>> ListObsLogsAsync()
    {
        try
        {
            var dir = ObsLogDirectory;
            if (!Directory.Exists(dir)) return Task.FromResult(new List<HostLogFile>());
            var files = new DirectoryInfo(dir)
                .EnumerateFiles("*.*")
                .Where(f => f.Extension is ".log" or ".txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(20)
                .Select(f => new HostLogFile
                {
                    Name = f.Name,
                    Path = f.FullName,
                    Size = f.Length,
                    Modified = new DateTimeOffset(f.LastWriteTime).ToUnixTimeMilliseconds()
                })
                .ToList();
            return Task.FromResult(files);
        }
        catch
        {
            return Task.FromResult(new List<HostLogFile>());
        }
    }

    /// <summary>读取指定日志文件内容；限定只能读取 OBS 日志目录内的 .txt/.log 文件。</summary>
    public Task<string?> ReadObsLogAsync(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(ObsLogDirectory);
            if (!IsInsideRoot(root, full)) return Task.FromResult<string?>(null);
            var ext = Path.GetExtension(full).ToLowerInvariant();
            if (ext is not (".txt" or ".log")) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>(File.ReadAllText(full));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    // ------------------------------------------------------------ 环境信息

    public Task<HostEnvironment> GetEnvironmentAsync()
    {
        var logDir = ObsLogDirectory;
        return Task.FromResult(new HostEnvironment
        {
            Platform = Platform,
            AppVersion = typeof(HostBridge).Assembly.GetName().Version?.ToString(3) ?? "",
            ObsLogDirectory = logDir,
            LogDirectoryExists = Directory.Exists(logDir)
        });
    }

    // ------------------------------------------------------------ 系统探测

    /// <summary>拉取本机系统环境。失败时返回 null。</summary>
    public Task<HostSystemInfo?> GetSystemInfoAsync()
    {
        try
        {
            var obsProc = FindObsProcesses().FirstOrDefault();
            double freeGb = 0, totalGb = 0;
            try
            {
                var drive = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .OrderBy(d => d.AvailableFreeSpace)
                    .FirstOrDefault();
                if (drive is not null)
                {
                    freeGb = drive.AvailableFreeSpace / 1073741824.0;
                    totalGb = drive.TotalSize / 1073741824.0;
                }
            }
            catch { }

            return Task.FromResult<HostSystemInfo?>(new HostSystemInfo
            {
                Platform = Platform,
                OsVersion = Environment.OSVersion.Version.ToString(),
                OsBuild = "",
                HagsEnabled = false,
                GameModeEnabled = false,
                Obs = new ObsProcessInfo
                {
                    Running = obsProc is not null,
                    Elevated = false,
                    CpuPercent = 0,
                    MemoryMb = obsProc is null ? 0 : obsProc.WorkingSet64 / 1048576.0,
                    Version = ""
                },
                Gpus = new(),
                PrimaryGpu = "",
                RecordingDiskFreeGb = freeGb,
                RecordingDiskTotalGb = totalGb
            });
        }
        catch
        {
            return Task.FromResult<HostSystemInfo?>(null);
        }
    }

    private static Process[] FindObsProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .Where(p =>
                {
                    try { return p.ProcessName is "OBS" or "obs64" or "obs32" or "OBS Studio"; }
                    catch { return false; }
                })
                .ToArray();
        }
        catch
        {
            return Array.Empty<Process>();
        }
    }

    /// <summary>查询 OBS Studio 最新发布版本（可选联网，失败返回 null）。</summary>
    public async Task<string?> GetObsLatestVersionAsync()
    {
        try
        {
            using var resp = await Http.GetAsync(
                "https://api.github.com/repos/obsproject/obs-studio/releases/latest");
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            return string.IsNullOrWhiteSpace(tag) ? null : tag.TrimStart('v');
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------ OBS 配置读取

    /// <summary>列出 OBS 配置目录（或其子目录）下的条目，用于发现 profiles / 场景集合。</summary>
    public Task<List<HostConfigEntry>> ListObsConfigAsync(string relativePath = "")
    {
        try
        {
            var root = Path.GetFullPath(ObsConfigRoot);
            var target = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!IsInsideRoot(root, target) && target != root)
                return Task.FromResult(new List<HostConfigEntry>());

            var list = new List<HostConfigEntry>();
            var di = new DirectoryInfo(target);
            if (!di.Exists) return Task.FromResult(list);

            foreach (var d in di.EnumerateDirectories())
                list.Add(new HostConfigEntry
                {
                    Name = d.Name,
                    IsDir = true,
                    Modified = new DateTimeOffset(d.LastWriteTime).ToUnixTimeMilliseconds()
                });
            foreach (var f in di.EnumerateFiles())
                list.Add(new HostConfigEntry
                {
                    Name = f.Name,
                    IsDir = false,
                    Size = f.Length,
                    Modified = new DateTimeOffset(f.LastWriteTime).ToUnixTimeMilliseconds()
                });
            return Task.FromResult(list.OrderBy(e => e.Name).ToList());
        }
        catch
        {
            return Task.FromResult(new List<HostConfigEntry>());
        }
    }

    /// <summary>读取 OBS 配置文件内容（限定在 obs-studio 目录内）。</summary>
    public Task<string?> ReadObsConfigAsync(string relativePath)
    {
        try
        {
            var root = Path.GetFullPath(ObsConfigRoot);
            var full = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!IsInsideRoot(root, full)) return Task.FromResult<string?>(null);
            if (!File.Exists(full)) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>(File.ReadAllText(full));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    // ------------------------------------------------------------ 云端 AI 转发

    /// <summary>
    /// 转发一次云端 AI 请求：API Key 由本类从机密存储取出并拼装 Authorization 头，
    /// 强制 https，且拒绝内网 / 本机 / 链路本地目标（防 SSRF：API Key 会随请求发出，
    /// 不能让钓鱼教程诱导用户把地址改成任意端点后把 Key 带走）。
    /// </summary>
    public async Task<string> AiChatAsync(string url, string secretKey, string body)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidOperationException("云端接口地址必须为 https。");

        if (await IsPrivateOrUnresolvableEndpointAsync(uri))
            throw new InvalidOperationException(
                "云端接口地址不允许指向本机、内网或链路本地地址（防 SSRF 保护，API Key 会随请求发送）。");

        var key = await GetSecretAsync(secretKey);
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException($"机密「{secretKey}」不存在或不可读，请先在 AI 设置中保存 API Key。");

        using var req = new HttpRequestMessage(HttpMethod.Post, uri);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var resp = await Http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"云端返回 {(int)resp.StatusCode}：{Truncate(text, 300)}");
        return text;
    }

    /// <summary>主机名解析不出、指向回环/私网/链路本地/保留段的地址一律拒绝。</summary>
    private static async Task<bool> IsPrivateOrUnresolvableEndpointAsync(Uri uri)
    {
        var host = uri.Host;
        if (host is "localhost" or "localhost.localdomain") return true;
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localdomain", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".home", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".arpa", StringComparison.OrdinalIgnoreCase)) return true;

        if (System.Net.IPAddress.TryParse(host, out var literal))
            return IsNonPublicIp(literal);

        try
        {
            var addrs = await System.Net.Dns.GetHostAddressesAsync(host);
            return addrs.Length == 0 || addrs.Any(IsNonPublicIp);
        }
        catch
        {
            return true; // 解析失败视为不可信
        }
    }

    private static bool IsNonPublicIp(System.Net.IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal) return true;
            if (ip.Equals(System.Net.IPAddress.IPv6Loopback) || ip.Equals(System.Net.IPAddress.IPv6None)) return true;
            if (ip.IsIPv4MappedToIPv6) return IsNonPublicIp(ip.MapToIPv4());
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] is 127 or 10 or 0
            || b[0] == 169 && b[1] == 254
            || b[0] == 192 && b[1] == 168
            || b[0] == 172 && b[1] >= 16 && b[1] <= 31
            || b[0] == 100 && b[1] >= 64 && b[1] <= 127   // CGNAT
            || b[0] >= 224;                                // 组播 / 保留段
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    /// <summary>用系统默认浏览器打开外链（仅 http/https）。</summary>
    public Task<bool> OpenExternalAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return Task.FromResult(false);
        if (uri.Scheme is not ("http" or "https")) return Task.FromResult(false);
        OpenInBrowser(uri.AbsoluteUri);
        return Task.FromResult(true);
    }

    // ------------------------------------------------------------ 导出 / 导入 / 备份 / 重置

    /// <summary>把场景模板 JSON 导出到用户目录（Downloads 优先）。失败返回 null。</summary>
    public Task<string?> ExportTemplateAsync(string filename, string json)
    {
        try
        {
            filename = Path.GetFileName(filename);
            if (string.IsNullOrWhiteSpace(filename)) filename = "scene-template.json";
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var dir = Directory.Exists(downloads) ? downloads : AppDataDir;
            var path = Path.Combine(dir, filename);
            File.WriteAllText(path, json);
            return Task.FromResult<string?>(path);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>定位 OBS 配置目录；overridePath 非空时为手动指定。</summary>
    public Task<HostConfigLocation?> LocateObsConfigAsync(string? overridePath = null)
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(overridePath) ? ObsConfigRoot : overridePath!;
            return Task.FromResult<HostConfigLocation?>(new HostConfigLocation
            {
                ConfigDir = dir,
                Exists = Directory.Exists(dir),
                Portable = false,
                Source = string.IsNullOrWhiteSpace(overridePath) ? "auto" : "override"
            });
        }
        catch
        {
            return Task.FromResult<HostConfigLocation?>(null);
        }
    }

    /// <summary>检测 OBS 是否正在运行。</summary>
    public Task<bool> IsObsRunningAsync() => Task.FromResult(FindObsProcesses().Length > 0);

    /// <summary>打包 OBS 配置为 zip。targetPath 为空时自动落到应用备份目录，返回实际路径。</summary>
    public Task<string?> PackObsConfigAsync(string? targetPath, bool includeKey, bool includePluginConfig, string reason)
    {
        try
        {
            var root = ObsConfigRoot;
            if (!Directory.Exists(root)) return Task.FromResult<string?>(null);
            var name = $"obs-config-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            var destDir = string.IsNullOrWhiteSpace(targetPath) ? BackupDir : targetPath;
            Directory.CreateDirectory(destDir);
            var path = Path.Combine(destDir, name);
            var tmp = Path.Combine(AppDataDir, name + ".tmp");
            if (File.Exists(tmp)) File.Delete(tmp);

            using (var archive = ZipFile.Open(tmp, ZipArchiveMode.Create))
                AddDirRecursive(archive, root, root, includeKey, includePluginConfig);

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return Task.FromResult<string?>(path);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static void AddDirRecursive(ZipArchive archive, string root, string current,
        bool includeKey, bool includePluginConfig)
    {
        foreach (var f in Directory.EnumerateFiles(current))
        {
            var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            var top = rel.Split('/')[0].ToLowerInvariant();
            if (!includeKey && top == "profiles" && rel.EndsWith("/service.json", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!includePluginConfig && top == "plugin_config") continue;
            archive.CreateEntryFromFile(f, rel, CompressionLevel.Optimal);
        }
        foreach (var d in Directory.EnumerateDirectories(current))
            AddDirRecursive(archive, root, d, includeKey, includePluginConfig);
    }

    /// <summary>导出 OBS 配置到 Downloads。失败返回 null。</summary>
    public async Task<string?> ExportObsConfigAsync(bool includeKey, bool includePluginConfig)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return await PackObsConfigAsync(
            Directory.Exists(downloads) ? downloads : AppDataDir,
            includeKey, includePluginConfig, "manual-export");
    }

    /// <summary>zip 条目里明显不属于 OBS 配置的文件类型（防导入可执行载荷）。</summary>
    private static readonly string[] ForbiddenImportExtensions =
    {
        ".sh", ".command", ".app", ".exe", ".bat", ".cmd", ".ps1", ".py", ".rb", ".pl",
        ".dylib", ".so", ".dll", ".bin", ".framework"
    };

    /// <summary>
    /// 从最近一份备份导入 OBS 配置。mode = overwrite | merge。
    /// 解包目标严格限定在 obs-studio 目录内，且拒绝可执行文件与超限条目（防 zip 炸弹）。
    /// OBS 正在运行时拒绝导入：运行中覆盖配置会产生新旧状态混杂。
    /// </summary>
    public async Task<HostImportResult?> ImportObsConfigAsync(string mode)
    {
        try
        {
            if (FindObsProcesses().Length > 0)
                return new HostImportResult
                { Ok = false, Message = "OBS 正在运行，请先完全退出 OBS 再执行导入。" };

            var latest = new DirectoryInfo(BackupDir)
                .EnumerateFiles("*.zip")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (latest is null)
                return new HostImportResult
                { Ok = false, Message = "没有可用的备份文件。" };

            var root = Path.GetFullPath(ObsConfigRoot);
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);

            var autoBackup = await PackObsConfigAsync(null, true, true, "auto-before-import");

            const long maxTotalBytes = 512L * 1024 * 1024;
            const int maxEntries = 20000;
            using var archive = ZipFile.OpenRead(latest.FullName);
            if (archive.Entries.Count > maxEntries)
                return new HostImportResult { Ok = false, Message = $"备份条目数异常（{archive.Entries.Count}），已取消导入。" };
            if (archive.Entries.Sum(e => (long)e.Length) > maxTotalBytes)
                return new HostImportResult { Ok = false, Message = "备份解压后体积超出限制（512 MB），已取消导入。" };

            int collections = 0, profiles = 0;
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (ForbiddenImportExtensions.Contains(ext)) continue;

                var dest = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!IsInsideRoot(root, dest)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: mode != "merge" || !File.Exists(dest));
                var norm = entry.FullName.Replace('\\', '/');
                if (norm.StartsWith("basic/scenes/", StringComparison.Ordinal)) collections++;
                if (norm.StartsWith("basic/profiles/", StringComparison.Ordinal)) profiles++;
            }
            return new HostImportResult
            {
                Ok = true,
                ImportedCollections = collections,
                ImportedProfiles = profiles,
                AutoBackupPath = autoBackup,
                Message = "导入完成。重启 OBS 后生效。"
            };
        }
        catch (Exception ex)
        {
            return new HostImportResult { Ok = false, Message = ex.Message };
        }
    }

    /// <summary>列出应用备份目录下的全部备份（按创建时间倒序）。</summary>
    public Task<List<HostBackupInfo>> ListObsBackupsAsync()
    {
        try
        {
            var list = new DirectoryInfo(BackupDir)
                .EnumerateFiles("*.zip")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(30)
                .Select(f => new HostBackupInfo
                {
                    Path = f.FullName,
                    CreatedAt = new DateTimeOffset(f.CreationTime).ToUnixTimeMilliseconds(),
                    Reason = "manual"
                })
                .ToList();
            return Task.FromResult(list);
        }
        catch
        {
            return Task.FromResult(new List<HostBackupInfo>());
        }
    }

    /// <summary>
    /// 彻底重置 OBS 配置（先自动备份，再把配置移入应用回收目录，永不硬删）。
    /// OBS 正在运行时拒绝执行：运行中移走配置目录会产生新旧状态混杂。
    /// </summary>
    public async Task<HostResetResult?> ResetObsConfigFullAsync()
    {
        try
        {
            var root = ObsConfigRoot;
            if (!Directory.Exists(root))
                return new HostResetResult { Ok = false, Message = "未找到 OBS 配置目录。" };

            if (FindObsProcesses().Length > 0)
                return new HostResetResult
                { Ok = false, Message = "OBS 正在运行，请先完全退出 OBS 再执行重置。" };

            var backup = await PackObsConfigAsync(null, true, true, "auto-before-reset");
            var dest = Path.Combine(TrashDir, $"obs-studio-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.Move(root, dest);
            return new HostResetResult
            {
                Ok = true,
                AutoBackupPath = backup,
                TrashPath = dest,
                Message = "已重置。原配置保存在：" + dest
            };
        }
        catch (Exception ex)
        {
            return new HostResetResult { Ok = false, Message = ex.Message };
        }
    }

    // ------------------------------------------------------------ 系统资源采样

    private static (DateTime At, TimeSpan Cpu) _lastCpuSample;

    /// <summary>拉取一次系统资源采样（CPU / 内存 / 磁盘）。best-effort。</summary>
    public Task<HostSystemSample?> GetSystemSampleAsync()
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            double cpuPercent;
            if (_lastCpuSample.At != default)
            {
                var cpuUsed = (proc.TotalProcessorTime - _lastCpuSample.Cpu).TotalSeconds;
                var wall = (now - _lastCpuSample.At).TotalSeconds;
                cpuPercent = wall > 0 ? Math.Clamp(cpuUsed / wall / Environment.ProcessorCount * 100.0, 0, 100) : 0;
            }
            else cpuPercent = 0;
            _lastCpuSample = (now, proc.TotalProcessorTime);

            var mem = GC.GetGCMemoryInfo();
            var memUsedMb = proc.WorkingSet64 / 1048576.0;
            var memTotalMb = mem.TotalAvailableMemoryBytes / 1048576.0;

            var disks = new List<HostDiskSample>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                try
                {
                    disks.Add(new HostDiskSample
                    {
                        Name = d.Name,
                        TotalGb = d.TotalSize / 1073741824.0,
                        FreeGb = d.AvailableFreeSpace / 1073741824.0
                    });
                }
                catch { }
            }

            return Task.FromResult<HostSystemSample?>(new HostSystemSample
            {
                CpuPercent = cpuPercent,
                MemUsedMb = memUsedMb,
                MemTotalMb = memTotalMb,
                MemUsedPercent = memTotalMb > 0 ? memUsedMb / memTotalMb * 100.0 : 0,
                NetDownKbps = 0,
                NetUpKbps = 0,
                Disks = disks
            });
        }
        catch
        {
            return Task.FromResult<HostSystemSample?>(null);
        }
    }

    // ------------------------------------------------------------ 应用更新检查

    /// <summary>查询本应用的 GitHub tags（按版本号降序排列）；失败或离线返回 null。</summary>
    public async Task<List<string>?> CheckAppUpdateAsync()
    {
        try
        {
            using var resp = await Http.GetAsync(
                "https://api.github.com/repos/YYRMMAYO/OBS-Helpmac/tags?per_page=10");
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            // tags 不保证按版本排序，必须显式解析版本号后降序排列，否则「最新版」判断可能错误
            return doc.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("name").GetString() ?? "")
                .Where(n => n.Length > 0)
                .OrderByDescending(n => ParseVersionLoose(n))
                .ToList();
        }
        catch
        {
            return null;
        }
    }

    private static Version ParseVersionLoose(string tag)
    {
        var core = tag.TrimStart('v', 'V');
        var cut = core.IndexOfAny(['-', '+']);
        if (cut > 0) core = core[..cut];
        return Version.TryParse(core, out var v) ? v : new Version(0, 0);
    }

    // ------------------------------------------------------------ Finder 显示

    /// <summary>在 Finder / 资源管理器中显示指定文件或目录。</summary>
    public Task<bool> RevealInFinderAsync(string path)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) Process.Start(new ProcessStartInfo("open", $"-R \"{path}\"") { UseShellExecute = false });
            else if (OperatingSystem.IsWindows()) Process.Start("explorer.exe", $"/select,\"{path}\"");
            else Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = false });
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>读取桌面壳偏好（当前为「关闭到托盘」开关；存键值存储）。</summary>
    public Task<HostShellPrefs?> GetShellPrefsAsync()
    {
        var raw = _store.Get("shell.prefs.closeToTray");
        return Task.FromResult<HostShellPrefs?>(new HostShellPrefs { CloseToTray = raw == "1" });
    }

    /// <summary>保存桌面壳偏好。</summary>
    public Task<bool> SetShellPrefsAsync(bool closeToTray)
    {
        _store.Set("shell.prefs.closeToTray", closeToTray ? "1" : "0");
        return Task.FromResult(true);
    }
}
