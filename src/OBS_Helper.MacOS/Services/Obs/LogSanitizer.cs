using System.Text;
using System.Text.RegularExpressions;

namespace OBS_Helper.MacOS.Services.Obs;

/// <summary>
/// 日志脱敏器（技术计划 §4.4「日志分析」与 §6「安全与隐私边界」）。
///
/// OBS 日志里可能出现推流密钥、账号邮箱、家庭宽带公网 IP、Windows 用户名等
/// 个人信息。凡是要展示给用户之外的对象（尤其是「复制到剪贴板」和「发给云端
/// AI」两条路径），都必须先经过这里。
///
/// 实现原则：
/// <list type="bullet">
///   <item><b>宁可多脱一点</b>：误伤一段无关字符串，代价远小于泄露一个推流密钥。</item>
///   <item><b>保留可诊断性</b>：只替换敏感片段，保留行号、时间戳、错误码和上下文，
///         脱敏后的日志依然能用来定位问题。</item>
///   <item><b>纯函数</b>：不依赖任何服务，便于单元测试覆盖。</item>
/// </list>
/// </summary>
public static class LogSanitizer
{
    private const string Mask = "[已隐藏]";
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // rtmp(s)/srt/rist 推流地址：保留协议与主机，抹掉后面的应用名与串流密钥
    private static readonly Regex StreamUrl = new(
        @"\b(rtmps?|srt|rist|ws|wss|http|https)://([^\s/:]+)(?::\d+)?(/\S*)?",
        Opts);

    // key=xxx / streamkey: xxx / token=xxx / password=xxx / secret=xxx
    private static readonly Regex KeyValueSecret = new(
        @"\b(stream[_-]?key|key|token|passwd|password|secret|auth|api[_-]?key|bearer)\b\s*[:=]\s*[""']?([^\s""',;)]+)",
        Opts);

    // 邮箱
    private static readonly Regex Email = new(
        @"\b[\w.+-]+@[\w-]+\.[\w.-]{2,}\b", Opts);

    // MAC 地址
    private static readonly Regex Mac = new(
        @"\b(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b", Opts);

    // IPv4（本机/未指定地址保留，方便判断是不是连的本地）
    private static readonly Regex Ipv4 = new(
        @"\b(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})\b", Opts);

    // Windows 用户目录
    private static readonly Regex WinUserPath = new(
        @"([A-Za-z]:\\Users\\)([^\\\s""']+)", Opts);

    // macOS / Linux 用户目录
    private static readonly Regex UnixUserPath = new(
        @"(/Users/|/home/)([^/\s""']+)", Opts);

    // 长串十六进制 / base64 样式的令牌（>= 24 位），常见于串流密钥
    private static readonly Regex LongToken = new(
        @"\b[A-Za-z0-9_\-]{24,}\b", RegexOptions.CultureInvariant);

    // IPv6 候选串（交给 IPAddress.TryParse 严格验证后再决定是否抹除）
    private static readonly Regex Ipv6Candidate = new(
        @"(?<![\w:.])(?:[A-Fa-f0-9]{0,4}:){2,7}[A-Fa-f0-9]{0,4}(?![\w:])", Opts);

    /// <summary>这些「长串」是 OBS 日志里的正常内容，不应被当成密钥抹掉。</summary>
    private static readonly string[] TokenAllowList =
    {
        "obs-studio", "libobs", "OBSBasic", "Microsoft", "NVIDIA", "AMD", "Intel",
        "GeForce", "Radeon", "Direct3D", "WindowsGraphicsCapture", "monitor_capture",
        "window_capture", "game_capture", "dshow_input", "wasapi_output_capture",
        "wasapi_input_capture", "browser_source", "text_gdiplus", "ffmpeg_muxer",
        "obs_x264", "jim_nvenc", "obs_qsv11", "h264_texture_amf", "av1_texture_amf",
        "screen_capture", "coreaudio_input_capture", "syphon-input", "mac-capture"
    };

    /// <summary>对整段日志做脱敏。</summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var sb = new StringBuilder(text.Length);
        foreach (var line in SplitLines(text))
        {
            sb.Append(SanitizeLine(line));
            sb.Append('\n');
        }
        // 去掉最后多加的换行
        if (sb.Length > 0 && sb[^1] == '\n') sb.Length--;
        return sb.ToString();
    }

    /// <summary>对单行做脱敏。顺序很重要：先处理结构化的，再处理泛化的。</summary>
    public static string SanitizeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;

        // 1) key=value 形式的密钥（最精确，先处理）
        line = KeyValueSecret.Replace(line, m => $"{m.Groups[1].Value}={Mask}");

        // 2) 推流 / 服务地址：保留主机名，抹掉路径（串流密钥通常在路径里）。
        //    主机名本身可能内嵌用户 / 房间标识（如 xxx-12345.live.xxx.com），含长数字段的标签一并抹除。
        line = StreamUrl.Replace(line, m =>
        {
            var scheme = m.Groups[1].Value;
            var host = MaskHostIfIdentifiable(m.Groups[2].Value);
            var path = m.Groups[3].Value;
            // 本地回环地址（跟 OBS 的 websocket 连接）完整保留，方便排查连接问题
            if (IsLoopbackHost(m.Groups[2].Value)) return m.Value;
            return string.IsNullOrEmpty(path) || path == "/"
                ? $"{scheme}://{host}"
                : $"{scheme}://{host}/{Mask}";
        });

        // 3) 邮箱 / MAC
        line = Email.Replace(line, Mask);
        line = Mac.Replace(line, Mask);

        // 4) 用户名路径
        line = WinUserPath.Replace(line, m => m.Groups[1].Value + "[用户]");
        line = UnixUserPath.Replace(line, m => m.Groups[1].Value + "[用户]");

        // 5) 公网 IPv4（保留私网与回环，它们对排查网络问题有用且不算隐私）
        line = Ipv4.Replace(line, m => IsPrivateOrLoopbackIpv4(m) ? m.Value : "[IP]");

        // 6) 公网 IPv6：macOS 网络日志常见，此前完全不处理属于外泄盲区。
        //    仅保留回环 / 未指定地址，其余（含链路本地 fe80——后 64 位由 MAC 派生）一律抹除。
        line = Ipv6Candidate.Replace(line, m =>
        {
            if (!System.Net.IPAddress.TryParse(m.Value, out var ip)) return m.Value;
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return m.Value;
            if (ip.Equals(System.Net.IPAddress.IPv6Loopback) || ip.Equals(System.Net.IPAddress.IPv6None))
                return m.Value;
            if (ip.IsIPv4MappedToIPv6)
                return IsPrivateOrLoopbackIpv4(ip.MapToIPv4()) ? m.Value : "[IPv6]";
            return "[IPv6]";
        });

        // 7) 剩下的超长令牌
        line = LongToken.Replace(line, m => IsAllowedToken(m.Value) ? m.Value : Mask);

        return line;
    }

    /// <summary>
    /// 主机名残留处理：包含 ≥4 位连续数字或超长标签的主机名，把该标签替换为占位符
    /// （推流平台常以「用户名-房间号」作为子域名，如 <c>xxx-12345.live.xxx.com</c>）。
    /// </summary>
    private static string MaskHostIfIdentifiable(string host)
    {
        var labels = host.Split('.');
        for (int i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            bool identifiable =
                (label.Length >= 4 && label.Any(char.IsAsciiDigit) && Enumerable.Range(0, label.Length - 3)
                    .Any(j => label.Skip(j).Take(4).All(char.IsAsciiDigit)))
                || label.Length > 20;
            if (identifiable)
            {
                labels[i] = "[主机]";
            }
        }
        var masked = string.Join('.', labels);
        return masked == host ? host : masked;
    }

    private static bool IsLoopbackHost(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host == "127.0.0.1"
        || host == "::1"
        || host == "[::1]";

    private static bool IsPrivateOrLoopbackIpv4(Match m)
    {
        if (!int.TryParse(m.Groups[1].Value, out var a) ||
            !int.TryParse(m.Groups[2].Value, out var b) ||
            !int.TryParse(m.Groups[3].Value, out var c) ||
            !int.TryParse(m.Groups[4].Value, out var d))
            return true; // 解析不出来说明不是 IP（比如版本号），保留原样

        if (a > 255 || b > 255 || c > 255 || d > 255) return true; // 版本号之类
        return IsPrivateOrLoopbackIpv4Bytes(a, b, c, d);
    }

    private static bool IsPrivateOrLoopbackIpv4(System.Net.IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b.Length != 4 || IsPrivateOrLoopbackIpv4Bytes(b[0], b[1], b[2], b[3]);
    }

    private static bool IsPrivateOrLoopbackIpv4Bytes(int a, int b, int c, int d)
    {
        if (a == 127 || a == 10 || a == 0) return true;
        if (a == 192 && b == 168) return true;
        if (a == 172 && b >= 16 && b <= 31) return true;
        if (a == 169 && b == 254) return true;
        if (a == 255) return true; // 子网掩码
        return false;
    }

    private static bool IsAllowedToken(string token)
    {
        // 纯数字（时间戳、字节数）不是密钥
        if (token.All(char.IsAsciiDigit)) return true;

        // 版本号 / 已知标识符：必须与整个 token 全词相等。
        // 之前按子串匹配，一个恰好包含 "Intel"/"AMD" 等字样的真实密钥会被漏掉。
        foreach (var allowed in TokenAllowList)
        {
            if (token.Equals(allowed, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // 全是字母且含有明显的英文单词分隔（下划线/连字符占比高）→ 多半是标识符而非密钥
        var separators = token.Count(ch => ch is '_' or '-');
        if (separators >= 2 && !token.Any(char.IsAsciiDigit)) return true;

        return false;
    }

    /// <summary>按行切分，不为整份日志分配一个巨大的字符串数组。</summary>
    public static IEnumerable<string> SplitLines(string text)
    {
        int start = 0;
        while (start <= text.Length)
        {
            int idx = text.IndexOf('\n', start);
            if (idx < 0)
            {
                if (start < text.Length) yield return text[start..].TrimEnd('\r');
                yield break;
            }
            yield return text[start..idx].TrimEnd('\r');
            start = idx + 1;
        }
    }
}
