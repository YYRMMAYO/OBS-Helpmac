namespace OBS_Helper.MacOS.Infrastructure;

/// <summary>
/// 极简文件日志（应用数据目录 /logs/app.log）。
/// 全项目大量 catch {} 此前完全不可观测，关键路径的失败至少要留痕。
/// 线程安全；日志自身失败绝不向上抛。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OBS_Helper", "logs", "app.log");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(Exception? ex, string context)
        => Write("ERROR", context, ex?.ToString());

    private static void Write(string level, string message, string? detail)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                if (!string.IsNullOrEmpty(detail)) line += Environment.NewLine + detail;
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // 日志不可写时静默放弃，绝不能影响主流程
        }
    }
}
