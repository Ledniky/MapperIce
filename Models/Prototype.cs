namespace MapperIce.Models;

public class Prototype
{
    public string Id { get; set; } = "";
    public string? SpritePath { get; set; }
    public string? RsiPath { get; set; }
    public string? State { get; set; }
    public string FilePath { get; set; } = "";
    public string? Parent { get; set; }
    public List<string> Components { get; set; } = new();
}