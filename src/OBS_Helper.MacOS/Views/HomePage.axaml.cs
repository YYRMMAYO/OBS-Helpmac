using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace OBS_Helper.MacOS.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private MainWindow? Shell =>
        this.GetVisualRoot() as MainWindow;

    private async Task LoadAsync()
    {
        var svc = App.Services;
        var data = await svc.Problems.GetDataAsync();
        var cats = await svc.Problems.GetCategoriesAsync();
        StatProblems.Text = data.Problems.Count.ToString();
        StatCategories.Text = cats.Count.ToString();

        try
        {
            var tpls = await svc.Templates.LoadAsync();
            StatTemplates.Text = tpls.Count.ToString();
        }
        catch { StatTemplates.Text = "—"; }

        var marks = await svc.Bookmarks.GetAllAsync();
        StatBookmarks.Text = marks.Count.ToString();

        CategoryList.ItemsSource = cats.Take(9).ToList();

        BookmarkPanel.Children.Clear();
        if (marks.Count == 0)
        {
            BookmarkPanel.Children.Add(new TextBlock
            {
                Classes = { "muted" },
                Text = "暂无收藏。浏览问题时点击「收藏」即可在这里找到。"
            });
        }
        else
        {
            foreach (var id in marks.Take(6))
            {
                var p = await svc.Problems.GetByIdAsync(id);
                if (p is null) continue;
                var btn = new Button
                {
                    Classes = { "ghost" },
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Content = new TextBlock
                    {
                        Text = p.Title,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        FontSize = 13
                    }
                };
                btn.Click += (_, _) => Shell?.OpenProblem(problemId: p.Id);
                BookmarkPanel.Children.Add(btn);
            }
        }
    }

    private void GoSearch(object? s, RoutedEventArgs e) => Shell?.NavigateTo("NavSearch");
    private void GoAssistant(object? s, RoutedEventArgs e) => Shell?.NavigateTo("NavAssistant");
    private void GoDiagnostic(object? s, RoutedEventArgs e) => Shell?.NavigateTo("NavDiagnostic");

    private void OnCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string catId)
            Shell?.OpenProblem(categoryId: catId);
    }
}
