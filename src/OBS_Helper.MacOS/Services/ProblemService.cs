using System.Reflection;
using System.Text.Json;
using OBS_Helper.MacOS.Models;

namespace OBS_Helper.MacOS.Services;

/// <summary>
/// 问题库服务：数据以内嵌资源（Assets/problems.json）随应用分发，完全离线可用。
/// </summary>
public class ProblemService
{
    private ProblemData? _data;
    private readonly object _gate = new();

    public Task<ProblemData> GetDataAsync()
    {
        if (_data is null)
        {
            lock (_gate)
            {
                _data ??= LoadEmbedded() ?? new ProblemData();
            }
        }
        return Task.FromResult(_data);
    }

    private static ProblemData? LoadEmbedded()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("problems.json", StringComparison.Ordinal));
            if (name is null) return null;
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) return null;
            using var sr = new StreamReader(s);
            return JsonSerializer.Deserialize<ProblemData>(sr.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<Category>> GetCategoriesAsync() => (await GetDataAsync()).Categories;

    public async Task<List<Problem>> GetProblemsAsync() => (await GetDataAsync()).Problems;

    public async Task<Problem?> GetByIdAsync(string id)
    {
        var data = await GetDataAsync();
        return data.Problems.FirstOrDefault(p => p.Id == id);
    }

    public async Task<Category?> GetCategoryAsync(string id)
    {
        var data = await GetDataAsync();
        return data.Categories.FirstOrDefault(c => c.Id == id);
    }

    public async Task<List<Problem>> GetByCategoryAsync(string categoryId)
    {
        var data = await GetDataAsync();
        return data.Problems.Where(p => p.Category == categoryId).ToList();
    }

    public async Task<List<Problem>> SearchAsync(string query)
    {
        var data = await GetDataAsync();
        if (string.IsNullOrWhiteSpace(query)) return data.Problems;
        var q = query.Trim().ToLowerInvariant();
        var catTitles = data.Categories.ToDictionary(c => c.Id, c => c.Title);
        return data.Problems
            .Where(p => BuildText(p, catTitles.GetValueOrDefault(p.Category, "")).Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static string BuildText(Problem p, string categoryTitle)
    {
        return string.Join(" ",
            new[] { p.Title, categoryTitle }
                .Concat(p.Symptoms)
                .Concat(p.Causes)
                .Concat(p.Steps.Select(s => s.Title + " " + s.Detail))
                .Concat(p.Tips)
                .Concat(p.Platforms));
    }
}
