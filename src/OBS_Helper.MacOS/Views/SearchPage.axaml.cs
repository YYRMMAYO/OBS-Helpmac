using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using OBS_Helper.MacOS.Models;
using OBS_Helper.MacOS.Services;

namespace OBS_Helper.MacOS.Views;

/// <summary>
/// 问题库（搜索 + 分类 + 详情）。与 Windows 版 CategoryPage / ProblemPage / SearchPage 对齐。
/// </summary>
public partial class SearchPage : UserControl
{
    private sealed class RowViewModel
    {
        public Problem Problem { get; init; } = new();
        public string Title => Problem.Title;
        public string MetaText { get; init; } = "";
    }

    private List<Category> _categories = new();
    private List<Problem> _all = new();
    private string? _categoryFilter;
    private Problem? _current;
    private bool _isBookmarked;

    public SearchPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_all.Count == 0) await LoadDataAsync();
        };
    }

    private async Task LoadDataAsync()
    {
        _categories = await App.Services.Problems.GetCategoriesAsync();
        _all = await App.Services.Problems.GetProblemsAsync();
        BuildCategoryChips();
        ApplyFilter(SearchBox.Text);
    }

    private void BuildCategoryChips()
    {
        CategoryChips.Children.Clear();
        AddChip("全部", null);
        foreach (var c in _categories) AddChip(c.Title, c.Id);
    }

    private void AddChip(string title, string? id)
    {
        var btn = new ToggleButton
        {
            Content = title,
            Classes = { "chip" },
            Tag = id,
            IsChecked = id == _categoryFilter
        };
        btn.Click += OnChipClick;
        CategoryChips.Children.Add(btn);
    }

    private async void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb) return;
        _categoryFilter = tb.IsChecked == true ? tb.Tag as string : null;
        foreach (ToggleButton chip in CategoryChips.Children)
            if (chip != tb && chip.IsChecked == true) chip.IsChecked = false;
        ApplyFilter(SearchBox.Text);
        await Task.CompletedTask;
    }

    public void FilterCategory(string categoryId)
    {
        _categoryFilter = categoryId;
        foreach (ToggleButton chip in CategoryChips.Children)
            chip.IsChecked = (string?)chip.Tag == categoryId;
        ApplyFilter(SearchBox.Text);
    }

    public async void ShowProblem(string problemId)
    {
        var p = await App.Services.Problems.GetByIdAsync(problemId);
        if (p is not null) RenderDetail(p);
    }

    private void ApplyFilter(string? query)
    {
        IEnumerable<Problem> source = string.IsNullOrWhiteSpace(_categoryFilter)
            ? _all
            : _all.Where(p => p.Category == _categoryFilter);

        List<Problem> results;
        if (string.IsNullOrWhiteSpace(query)) results = source.ToList();
        else
        {
            var q = query.Trim().ToLowerInvariant();
            results = source
                .Where(p => BuildText(p).Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        ResultList.ItemsSource = results.Select(p => new RowViewModel
        {
            Problem = p,
            MetaText = $"{CategoryTitle(p.Category)} · {p.Severity} · {p.Steps.Count} 步"
        }).ToList();

        ResultCount.Text = $"{results.Count} 条结果";
    }

    private static string BuildText(Problem p) =>
        string.Join(" ", new[] { p.Title }.Concat(p.Symptoms).Concat(p.Causes)
            .Concat(p.Steps.Select(s => s.Title + " " + s.Detail)).Concat(p.Tips));

    private string CategoryTitle(string id) =>
        _categories.FirstOrDefault(c => c.Id == id)?.Title ?? "未分类";

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter(SearchBox.Text);

    private void OnProblemActivated(object? sender, TappedEventArgs e)
    {
        if ((ResultList.ItemsSource as IEnumerable<RowViewModel>)?.ToList() is not { } rows) return;
        var idx = ResultList.SelectedIndex;
        if (idx < 0 || idx >= rows.Count) return;
        RenderDetail(rows[idx].Problem);
    }

    private async void RenderDetail(Problem p)
    {
        _current = p;
        EmptyState.IsVisible = false;

        DetailHeaderCard.IsVisible = true;
        DetailTitle.Text = p.Title;
        SeverityText.Text = p.Severity;
        SeverityPill.Background = p.Severity switch
        {
            "严重" => this.FindResource("DangerBrush") as Avalonia.Media.IBrush,
            "一般" => (Avalonia.Media.IBrush?)this.FindResource("WarningBrush"),
            _ => (Avalonia.Media.IBrush?)this.FindResource("AccentSoftBrush")
        };

        _isBookmarked = await App.Services.Bookmarks.IsBookmarkedAsync(p.Id);
        BookmarkBtn.Content = _isBookmarked ? "已收藏" : "收藏";

        SymptomsCard.IsVisible = p.Symptoms.Length > 0;
        SymptomList.ItemsSource = p.Symptoms;

        StepsCard.IsVisible = p.Steps.Count > 0;
        StepList.ItemsSource = p.Steps;

        TipsCard.IsVisible = p.Tips.Length > 0;
        TipList.ItemsSource = p.Tips;

        LinksCard.IsVisible = p.Links.Count > 0;
        LinkList.ItemsSource = p.Links;
    }

    private async void OnToggleBookmark(object? sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        await App.Services.Bookmarks.ToggleAsync(_current.Id);
        _isBookmarked = !_isBookmarked;
        BookmarkBtn.Content = _isBookmarked ? "已收藏" : "收藏";
    }

    private async void OnOpenLink(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string url)
            await App.Services.Host.OpenExternalAsync(url);
    }
}
