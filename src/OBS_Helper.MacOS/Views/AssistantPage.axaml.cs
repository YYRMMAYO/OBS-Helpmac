using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OBS_Helper.MacOS.Models;
using Avalonia.VisualTree;
using OBS_Helper.MacOS.Services;
using OBS_Helper.MacOS.Services.Ai;

namespace OBS_Helper.MacOS.Views;

/// <summary>AI 助手页（与 Windows 版 AssistantPage 对齐）：本地引擎默认，云端可选。</summary>
public partial class AssistantPage : UserControl
{
    private static readonly string[] DefaultSuggestions =
    {
        "直播时画面卡顿掉帧",
        "OBS 打不开或闪退",
        "推流失败 / 断流重连",
        "麦克风没有声音",
        "录制文件损坏打不开"
    };

    public AssistantPage()
    {
        InitializeComponent();
        SuggestList.ItemsSource = DefaultSuggestions;
        Loaded += (_, _) => RefreshEnginePill();
        App.Services.AiSettings.Changed += () =>
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshEnginePill);
    }

    private void RefreshEnginePill()
    {
        var ai = App.Services.AiSettings;
        EngineText.Text = ai.Mode == DiagnosticEngineMode.Cloud ? "云端引擎" : "本地引擎";
    }

    private async void OnAsk(object? sender, RoutedEventArgs e) => await AskAsync();

    private async void OnEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await AskAsync();
    }

    private async void OnSuggestion(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Content is TextBlock tb)
        {
            QueryBox.Text = tb.Text ?? "";
            await AskAsync();
        }
    }

    private async Task AskAsync()
    {
        var q = QueryBox.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return;

        AskBtn.IsEnabled = false;
        SuggestCard.IsVisible = false;
        AnswerPanel.Children.Clear();

        AddBubble("你的问题", q);

        try
        {
            var matches = await App.Services.Assistant.AskAsync(q);
            if (matches.Count == 0)
            {
                AddBubble("助手", "没有找到匹配的问题。换个说法，或者到「诊断」页做一次完整体检。");
            }
            else
            {
                var top = matches[0];
                string text =
                    $"最可能的问题：{top.Problem.Title}（匹配度 {top.Score}%）\n\n" +
                    string.Join("\n", top.Problem.Steps.Take(4).Select((s, i) => $"{i + 1}. {s.Title}：{s.Detail}")) +
                    (matches.Count > 1
                        ? $"\n\n其它候选：" + string.Join("、", matches.Skip(1).Take(3).Select(m => m.Problem.Title))
                        : "");
                AddBubble("助手建议", text);
                AddOpenProblemAction(top.Problem.Id);
            }
        }
        catch (Exception ex)
        {
            AddBubble("出错了", ex.Message);
        }
        finally
        {
            AskBtn.IsEnabled = true;
        }
    }

    private void AddBubble(string title, string body)
    {
        AnswerPanel.Children.Add(new Border
        {
            Classes = { "card" },
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    new TextBlock { Classes = { "body" }, Text = body, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
                }
            }
        });
    }

    private void AddOpenProblemAction(string problemId)
    {
        var btn = new Button { Classes = { "ghost" }, Content = new TextBlock { Text = "查看完整方案 →", Foreground = Avalonia.Media.Brushes.DodgerBlue } };
        btn.Click += (_, _) => (this.GetVisualRoot() as MainWindow)?.OpenProblem(problemId: problemId);
        AnswerPanel.Children.Add(btn);
    }
}
