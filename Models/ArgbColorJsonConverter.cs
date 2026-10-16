using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapperIce.Models;

/// <summary>
/// System.Drawing.Color для .ice-файлов: пишется строкой "A,R,G,B" (как прочие
/// цвета проекта — GridData.Color, RoomGridData.FillColor) и читается обратно.
///
/// Зачем конвертер: у System.Drawing.Color все свойства (R/G/B/A/Name/...) только
/// для чтения, поэтому System.Text.Json, сериализовав его сам, прочитать обратно
/// не может — он отдаёт default(Color), то есть Color.Empty с A=R=G=B=0. Из-за
/// этого запечённый в PipeEntity.CustomColor цвет трубы терялся при загрузке
/// проекта, а следующее сохранение записывало в файл нули. Старый плоский вид
/// {"R":..,"G":..,"B":..,"A":..} читается здесь же — чтобы файлы, сохранённые до
/// этой правки, продолжали загружаться (цвет из них уже не восстановить, он
/// приходит нулевым — см. бэкфилл в MainForm.LoadProject).
/// </summary>
public class ArgbColorJsonConverter : JsonConverter<Color>
{
    // Пусть null в поле-цвете (в старых файлах) не роняет разбор всей сущности:
    // без этого исключение внутри JsonSerializer.Deserialize приводило к пропуску
    // записи, а не к цвету по умолчанию.
    public override bool HandleNull => true;

    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var parts = raw.Split(',');
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out var a) &&
                    int.TryParse(parts[1], out var r) &&
                    int.TryParse(parts[2], out var g) &&
                    int.TryParse(parts[3], out var b))
                {
                    return Color.FromArgb(a, r, g, b);
                }
            }
            return Color.Empty;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int a = 0, r = 0, g = 0, b = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                var name = reader.GetString();
                reader.Read();
                switch (name)
                {
                    case "A": a = reader.GetInt32(); break;
                    case "R": r = reader.GetInt32(); break;
                    case "G": g = reader.GetInt32(); break;
                    case "B": b = reader.GetInt32(); break;
                    default: reader.Skip(); break;
                }
            }
            return Color.FromArgb(a, r, g, b);
        }

        reader.Skip();
        return Color.Empty;
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value.A},{value.R},{value.G},{value.B}");
    }
}
