namespace MapperIce.Models;

public class Repository
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsIndexed { get; set; } = false;
    public DateTime LastIndexed { get; set; }
    public int PrototypeCount { get; set; }
    
    public override string ToString() => Name;
}