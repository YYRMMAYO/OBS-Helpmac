namespace OBS_Helper.MacOS.Models;

public class ProblemData
{
    public string Version { get; set; } = "";
    public string Updated { get; set; } = "";
    public List<Category> Categories { get; set; } = new();
    public List<Problem> Problems { get; set; } = new();
}
