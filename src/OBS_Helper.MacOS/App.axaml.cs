using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OBS_Helper.MacOS.Infrastructure;
using OBS_Helper.MacOS.Services;
using OBS_Helper.MacOS.Services.Ai;
using OBS_Helper.MacOS.Services.Host;
using OBS_Helper.MacOS.Services.Obs;
using OBS_Helper.MacOS.Services.ObsConfig;
using OBS_Helper.MacOS.Views;

namespace OBS_Helper.MacOS;

public partial class App : Application
{
    /// <summary>应用级服务定位器（与 Windows 版 AppServices 对应）。</summary>
    public static AppServices Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = new AppServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();

        // 启动初始化的失败必须留痕：自动连接失败等此前完全静默
        _ = Task.Run(async () =>
        {
            try
            {
                await Services.InitializeAsync();
            }
            catch (Exception ex)
            {
                Infrastructure.AppLog.Error(ex, "应用启动初始化失败（自动连接 / 设置加载）");
            }
        });
    }
}

/// <summary>
/// 服务组装（单例）。与 Windows 版 AppServices / Web 版依赖注入容器同构，
/// 便于逐项对照两边功能。
/// </summary>
public sealed class AppServices
{
    public KeyValueStore Store { get; } = new();
    public HostBridge Host { get; }
    public ProblemService Problems { get; }
    public AssistantService Assistant { get; }
    public BookmarkService Bookmarks { get; }
    public ObsSettingsService ObsSettings { get; }
    public ObsConnectionService Obs { get; }
    public LiveMonitorService Monitor { get; }
    public SystemHealthService SystemHealth { get; }
    public ObsConfigScanner ConfigScanner { get; }
    public AiSettingsService AiSettings { get; }
    public ObsToolRegistry Tools { get; }
    public LocalDiagnosticEngine LocalEngine { get; }
    public CloudDiagnosticEngine CloudEngine { get; }
    public DiagnosticOrchestrator Diagnostic { get; }
    public SceneTemplateService Templates { get; }
    public ObsConfigService ObsConfig { get; }

    public AppServices()
    {
        Host = new HostBridge(Store);
        Problems = new ProblemService();
        Assistant = new AssistantService(Problems);
        Bookmarks = new BookmarkService(Store);
        ObsSettings = new ObsSettingsService(Store, Host);
        Obs = new ObsConnectionService(ObsSettings);
        Monitor = new LiveMonitorService(Obs);
        SystemHealth = new SystemHealthService(Host);
        ConfigScanner = new ObsConfigScanner(Host);
        AiSettings = new AiSettingsService(Store, Host);
        Tools = new ObsToolRegistry(Problems);
        LocalEngine = new LocalDiagnosticEngine(Problems, Assistant);
        CloudEngine = new CloudDiagnosticEngine(AiSettings, Host, Tools);
        Diagnostic = new DiagnosticOrchestrator(
            AiSettings, Obs,
            new ObsLogAnalyzer(),
            Problems, Assistant, Host, Tools, LocalEngine, CloudEngine,
            SystemHealth, ConfigScanner);
        Templates = new SceneTemplateService(Obs, Host);
        ObsConfig = new ObsConfigService(Host, Obs, Store);
    }

    /// <summary>启动时加载持久化设置（连接配置、AI 设置）。</summary>
    public async Task InitializeAsync()
    {
        await ObsSettings.LoadAsync();
        await AiSettings.LoadAsync();
        if (ObsSettings.Current.AutoConnect)
        {
            var password = ObsSettings.Current.RememberPassword
                ? await ObsSettings.GetPasswordAsync() : null;
            await Obs.ConnectAsync(password);
        }
    }
}
