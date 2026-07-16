// Models/DeleteSettings.cs
namespace MapperIce.Models;

public class DeleteSettings
{
    public bool DeleteAll { get; set; } = true;
    public bool DeletePipes { get; set; } = true;
    public bool DeleteWires { get; set; } = false;
    public bool DeleteEntities { get; set; } = false;
    public bool DeleteAlarms { get; set; } = true;
    
    public static DeleteSettings Default = new DeleteSettings();
}