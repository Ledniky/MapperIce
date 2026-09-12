// Models/WireEntity.cs
namespace MapperIce.Models;

public class WireEntity : MapEntity
{
    public string WireType { get; set; } = "HV"; // "HV" (ВВ), "MV" (СВ), "LV" (НВ)
}