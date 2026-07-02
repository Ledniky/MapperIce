// Models/PipeEntity.cs
namespace MapperIce.Models;

public class PipeEntity : MapEntity
{
    public string PipeType { get; set; } = "Distra";
    public bool IsEndpoint { get; set; } = false; // true - конец трубы
}