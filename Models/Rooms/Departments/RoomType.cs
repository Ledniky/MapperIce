using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ КЛАСС (все комнаты наследуются от него)
// ============================================================

public abstract class RoomType
{
    public abstract string Name { get; }
    /// <summary>Отображаемое имя типа комнаты на русском языке (только для UI)</summary>
    public virtual string DisplayName => Name;
    public virtual string Description => "";
    public virtual string Pack => "Vanilla";
    public virtual string Category => "Common";
    public virtual string WallProto => "WallSolid";
    public virtual string FloorProto => "FloorSteel";
    public virtual string DoorProto => "Airlock";
    public virtual string GlassDoorProto => "AirlockGlass";
    public virtual string? AirAlarmProto => null;  
    public virtual string? FireAlarmProto => null;
    /// <summary>Прототип голопада (id из holopad.yml), привязанный к этому типу комнаты в свойствах.</summary>
    public virtual string? HolopadProto => null;
    /// <summary>Прототип шкафчика (id из Catalog/Fills/Lockers), привязанный к этому типу комнаты в свойствах.</summary>
    public virtual string? LockerProto => null;
    /// <summary>Прототип хранилища скафандра (id из suit_storage.yml), привязанный к этому типу комнаты в свойствах.</summary>
    public virtual string? SuitStorageProto => null;
    /// <summary>Прототип кровати (id из beds.yml), привязанный к этому типу комнаты в свойствах.</summary>
    public virtual string? BedProto => null;
    /// <summary>Прототип одеяла (id из bedsheets.yml), привязанный к этому типу комнаты в свойствах.</summary>
    public virtual string? BedsheetProto => null;
    /// <summary>
    /// Прототип комода (id из Catalog/Fills/Lockers/dressers.yml), привязанный к этому
    /// типу комнаты в свойствах. Задан только у комнат глав отделов ("только главам"),
    /// у остальных типов — null, и привязка комода не ставится.
    /// </summary>
    public virtual string? DresserProto => null;
    /// <summary>
    /// Прототип камеры (id из surveillance_camera.yml), привязанный к этому типу комнаты.
    /// Отдел камеры совпадает с отделом комнаты, поэтому по умолчанию прототип
    /// подбирается автоматически по Category (см. CameraProtoForCategory); тип комнаты
    /// может переопределить его вручную.
    /// </summary>
    public virtual string? CameraProto => CameraProtoForCategory(Category);

    /// <summary>
    /// Прототип интеркома (id из intercom.yml), привязанный к этому типу комнаты.
    /// Как и у камеры, отдел интеркома совпадает с отделом комнаты, поэтому прототип
    /// по умолчанию подбирается автоматически по Category; тип комнаты может
    /// переопределить его вручную. Интерком при этом вешается В тайл-стену рядом
    /// с комнатой (см. WallMountOrientation).
    /// </summary>
    public virtual string? IntercomProto => IntercomProtoForCategory(Category);

    /// <summary>
    /// Адрес факса, привязанного к этому типу комнаты (значение поля name компонента
    /// FaxMachine, например "Medical" или "HoP Office"). Задан только у комнат, которым
    /// факс реально положен (список адресов снят с эталонных карт); у остальных типов —
    /// null, и привязка факса не ставится. Адрес запекается в FaxMachineEntity при
    /// установке, поэтому правка строки не переписывает уже расставленные факсы.
    /// </summary>
    public virtual string? FaxName => null;

    /// <summary>
    /// Прототип факса (id из fax_machine.yml). По умолчанию обычный FaxMachineBase;
    /// рубка капитана переопределяет его на FaxMachineCaptain (как на эталонных картах).
    /// </summary>
    public virtual string FaxProto => "FaxMachineBase";

    /// <summary>Прототип обычной лампы (id из light_*.yml), привязываемый к типу комнаты. По умолчанию Poweredlight.</summary>
    public virtual string LightProto => "Poweredlight";

    /// <summary>Прототип аварийной лампы (id из light_*.yml), привязываемый к типу комнаты. По умолчанию EmergencyLight.</summary>
    public virtual string EmergencyLightProto => "EmergencyLight";

    /// <summary>
    /// Интерком по отделу комнаты: у каждого отдела свой ключ шифрования
    /// (Intercom{Отдел} в intercom.yml). Supply в прототипах — это Cargo,
    /// Common — общий IntercomCommon. Комнатам без отдела (Antags) интерком
    /// не привязывается.
    /// </summary>
    public static string? IntercomProtoForCategory(string category)
    {
        return category.TrimEnd('+') switch
        {
            "Engineering" => "IntercomEngineering",
            "Security" => "IntercomSecurity",
            "Science" => "IntercomScience",
            "Cargo" => "IntercomSupply",
            "Command" => "IntercomCommand",
            "Service" => "IntercomService",
            "Medical" => "IntercomMedical",
            "Antags" => null,
            _ => "IntercomCommon"
        };
    }

    /// <summary>
    /// Камера по отделу комнаты: у каждого отдела СС14 своя подсеть камер
    /// (SurveillanceCamera{Отдел} в surveillance_camera.yml, частоты 1931-1938).
    /// Supply в прототипах — это Cargo, Common/General — общая сеть. Комнатам без
    /// отдела (Antags) камера не привязывается.
    /// </summary>
    public static string? CameraProtoForCategory(string category)
    {
        return category.TrimEnd('+') switch
        {
            "Engineering" => "SurveillanceCameraEngineering",
            "Security" => "SurveillanceCameraSecurity",
            "Science" => "SurveillanceCameraScience",
            "Cargo" => "SurveillanceCameraSupply",
            "Command" => "SurveillanceCameraCommand",
            "Service" => "SurveillanceCameraService",
            "Medical" => "SurveillanceCameraMedical",
            "Antags" => null,
            _ => "SurveillanceCameraGeneral"
        };
    }
    public virtual Color FillColor => Color.FromArgb(100, 230, 230, 230);
    public virtual Color LineColor => Color.FromArgb(255, 180, 180, 180);
    public virtual bool IsCustom => false;
    public virtual bool IsHidden => false;
    public virtual int Priority => 0;

    /// <summary>
    /// Группа сортировки внутри категории: обычные комнаты идут сначала (0),
    /// атмосные — после всех инженерных (1) и т.д. Используется в списках типов
    /// комнат (комбобокс, дерево диалога) как второй ключ после Category,
    /// чтобы атмос-комнаты не перемешивались с инженерными.
    /// </summary>
    public virtual int SortGroup => 0;
}

// ============================================================
// БАЗОВЫЙ ДЛЯ ОБЩИХ ТИПОВ (Common)
// ============================================================

public abstract class CommonRoomType : RoomType
{
    public override string Category => "Common";
    public override Color LineColor => Color.FromArgb(255, 180, 180, 180);
    public override Color FillColor => Color.FromArgb(100, 220, 220, 220);
}

// ============================================================
// БАЗОВЫЙ ДЛЯ АНТАГОНИСТОВ (Antags)
// ============================================================

public abstract class AntagRoomType : RoomType
{
    public override string Category => "Antags";
    public override string FloorProto => "FloorSteel";
}

// ============================================================
// ОБЩИЕ ТИПЫ (Common) - коридоры, технические и базовые
// ============================================================

public class General : CommonRoomType
{
    public override string Name => "General";
    public override string DisplayName => "Общее";
    public override string Description => "Общая комната без специализации. Коридоры, холлы и базовые помещения.";
    public override string? HolopadProto => "HolopadGeneralLounge";
    public override string? LockerProto => "ClosetMaintenanceFilledRandom";
    public override string? BedProto => "Bed";
    public override int Priority => 0;
}

public class Vox : CommonRoomType
{
    public override string Name => "Vox";
    public override string DisplayName => "Вокс";
    public override string Description => "Комната для рассы воксов. Оснащена отдельной воздушной сигнализацией.";
    public override string? AirAlarmProto => "AirAlarmVox";
    public override string? HolopadProto => "HolopadGeneralLounge";
    public override Color FillColor => Color.FromArgb(100, 254, 1, 230);
    public override Color LineColor => Color.FromArgb(100, 254, 1, 180);
    public override int Priority => 15;
}

public class Technical : CommonRoomType
{
    public override string Name => "Technical";
    public override string DisplayName => "Техи";
    public override string Description => "Техническое помещение. Обшитое деревянными панелями, с усиленными дверями.";
    public override string DoorProto => "AirlockMaintLocked";
    public override string FloorProto => "Plating";
    public override string GlassDoorProto => "AirlockMaintGlassLocked";
    public override string? HolopadProto => "HolopadGeneralTools";
    public override string? LockerProto => "ClosetMaintenanceFilledRandom";
    // Техи освещаются маленькими лампами (как на эталонных картах).
    public override string LightProto => "PoweredSmallLight";
    public override Color FillColor => Color.FromArgb(100, 255, 240, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
    public override int Priority => 0;
}

public class Hallway : CommonRoomType
{
    public override string Name => "Hallway";
    public override string DisplayName => "Коридор";
    public override string Description => "Коридор. Светлое помещение с кафельным полом для основных проходов.";
    public override string? HolopadProto => "HolopadGeneralTools";
    public override int Priority => 0;
}

public class BaseRoom : CommonRoomType
{
    public override string Name => "BaseRoom";
    public override string DisplayName => "Базовое";
    public override bool IsHidden => true;
    public override int Priority => 0;
}

// ============================================================
// АНТАГОНИСТЫ (Antags)
// ============================================================

public class Syndicate : AntagRoomType
{
    public override string Name => "Syndicate";
    public override string DisplayName => "Синдикат";
    public override string Description => "Комната синдиката. Бандитское помещение с усиленными дверями для тайных операций.";
    public override string DoorProto => "AirlockSyndicateLocked";
    public override string GlassDoorProto => "AirlockSyndicateGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 200, 50, 50);
    public override string? SuitStorageProto => "SuitStorageEVASyndicate";
    public override string? BedProto => "Bed";
    public override string? BedsheetProto => "BedsheetSyndie";
    public override int Priority => 250;
}

public class Nukeop : AntagRoomType
{
    public override string Name => "Nukeop";
    public override string DisplayName => "Ядерная операция";
    public override string Description => "Ядерная операционная комната. Особое помещение для ядерной команды с максимальной изоляцией.";
    public override string DoorProto => "AirlockSyndicateNukeopLocked";
    public override string GlassDoorProto => "AirlockSyndicateNukeopGlassLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 30, 30);
    public override Color LineColor => Color.FromArgb(255, 200, 30, 30);
    public override string? SuitStorageProto => "SuitStorageSyndie";
    public override int Priority => 300;
}

// ============================================================
// КАСТОМНЫЙ ТИП
// ============================================================

public class CustomRoomType : RoomType
{
    public CustomRoomTypeData Data { get; }
    public CustomRoomType(CustomRoomTypeData data)
    {
        Data = data;
    }

    public override int Priority => Data.Priority;

    public override string Name => Data.Name;
    public override string DisplayName => string.IsNullOrEmpty(Data.DisplayName) ? Data.Name : Data.DisplayName;
    public override string Description => string.IsNullOrEmpty(Data.Description) ? $"Кастомный тип: {Data.Name}" : Data.Description;
    public override string Pack => Data.Pack;
    public override string Category => Data.Category;
    public override string WallProto => Data.WallProto;
    public override string FloorProto => Data.FloorProto;
    public override string DoorProto => Data.DoorProto;
    public override string GlassDoorProto => Data.GlassDoorProto;
    public override string? AirAlarmProto => string.IsNullOrEmpty(Data.AirAlarmProto) ? null : Data.AirAlarmProto;
    public override string? FireAlarmProto => string.IsNullOrEmpty(Data.FireAlarmProto) ? null : Data.FireAlarmProto;
    public override string? HolopadProto => string.IsNullOrEmpty(Data.HolopadProto) ? null : Data.HolopadProto;
    public override string? LockerProto => string.IsNullOrEmpty(Data.LockerProto) ? null : Data.LockerProto;
    public override string? SuitStorageProto => string.IsNullOrEmpty(Data.SuitStorageProto) ? null : Data.SuitStorageProto;
    public override string? BedProto => string.IsNullOrEmpty(Data.BedProto) ? null : Data.BedProto;
    public override string? BedsheetProto => string.IsNullOrEmpty(Data.BedsheetProto) ? null : Data.BedsheetProto;
    public override string? DresserProto => string.IsNullOrEmpty(Data.DresserProto) ? null : Data.DresserProto;
    /// <summary>
    /// Камера кастомного типа: если в свойствах типа прототип не задан, он берётся
    /// автоматически по отделу (Category) — как у ванильных типов.
    /// </summary>
    public override string? CameraProto => string.IsNullOrEmpty(Data.CameraProto) ? base.CameraProto : Data.CameraProto;
    /// <summary>
    /// Интерком кастомного типа: если в свойствах прототип не задан, он берётся
    /// автоматически по отделу (Category) — как у ванильных типов.
    /// </summary>
    public override string? IntercomProto => string.IsNullOrEmpty(Data.IntercomProto) ? base.IntercomProto : Data.IntercomProto;
    /// <summary>
    /// Факс кастомного типа: адрес и прототип берутся из свойств типа, если заданы;
    /// иначе адреса нет (факс не ставится), а прототип — обычный FaxMachineBase.
    /// </summary>
    public override string? FaxName => string.IsNullOrEmpty(Data.FaxName) ? null : Data.FaxName;
    public override string FaxProto => string.IsNullOrEmpty(Data.FaxProto) ? base.FaxProto : Data.FaxProto;
    public override Color FillColor => ParseColor(Data.FillColor);
    public override Color LineColor => ParseColor(Data.LineColor);
    public override bool IsCustom => true;

    private static Color ParseColor(string value)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 4)
                return Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        }
        catch { }
        return Color.FromArgb(200, 230, 230, 230);
    }
}

// ============================================================
// МОДЕЛИ ДЛЯ ХРАНЕНИЯ
// ============================================================

public class CustomRoomTypeData
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Pack { get; set; } = "Custom";
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string? AirAlarmProto { get; set; } = null;
    public string? FireAlarmProto { get; set; } = null;
    public string? HolopadProto { get; set; } = null;
    public string? LockerProto { get; set; } = null;
    public string? SuitStorageProto { get; set; } = null;
    public string? BedProto { get; set; } = null;
    public string? BedsheetProto { get; set; } = null;
    public string? DresserProto { get; set; } = null;
    public string? CameraProto { get; set; } = null;
    public string? IntercomProto { get; set; } = null;
    /// <summary>Адрес факса (name компонента FaxMachine) и его прототип — для кастомных типов.</summary>
    public string? FaxName { get; set; } = null;
    public string? FaxProto { get; set; } = null;
    public string? LightProto { get; set; } = null;
    public string? EmergencyLightProto { get; set; } = null;
    public string Description { get; set; } = "";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public int Priority { get; set; } = 0;
}

public class ExportData
{
    public string Type { get; set; } = "Single"; // "Single" | "Category" | "Pack"
    public string Name { get; set; } = "";
    public string Pack { get; set; } = "Custom";
    public string Category { get; set; } = "Custom";
    public string CategoryColor { get; set; } = "255,136,136,136";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "Airlock";
    public string GlassDoorProto { get; set; } = "AirlockGlass";
    public string? HolopadProto { get; set; }
    public string? LockerProto { get; set; }
    public string? SuitStorageProto { get; set; }
    public string? BedProto { get; set; }
    public string? BedsheetProto { get; set; }
    public string? DresserProto { get; set; }
    public string? CameraProto { get; set; }
    public string? IntercomProto { get; set; }
    public string? FaxName { get; set; }
    public string? FaxProto { get; set; }
    public string? LightProto { get; set; }
    public string? EmergencyLightProto { get; set; }
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
    public int Priority { get; set; } = 0;
}