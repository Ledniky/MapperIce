// Models/PipeEntity.cs
using System.Drawing;

namespace MapperIce.Models;

public class PipeEntity : MapEntity
{
    public string PipeType { get; set; } = "Distra";
    public bool IsEndpoint { get; set; } = false; // true - конец трубы
    public int UtilArrowRotation { get; set; } = 0; // 0=юг, 1=запад, 2=север, 3=восток
    public Color? CustomColor { get; set; } = null; // нестандартный цвет трубы
}