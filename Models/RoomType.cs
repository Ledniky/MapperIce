using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ КЛАСС (все типы наследуются от него)
// ============================================================

public abstract class RoomType
{
    public abstract string Name { get; }
    public virtual string Category => "Common";
    public virtual string WallProto => "WallSolid";
    public virtual string FloorProto => "Plating";
    public virtual string DoorProto => "AirlockGlass";
    public virtual Color FillColor => Color.FromArgb(100, 230, 230, 230);
    public virtual Color LineColor => Color.FromArgb(255, 180, 180, 180);
    public virtual bool IsCustom => false;
    public virtual bool IsHidden => false;
}

// ============================================================
// БАЗОВЫЙ ДЛЯ ОБЩИХ ТИПОВ (Common)
// ============================================================

public abstract class CommonRoomType : RoomType
{
    public override string Category => "Common";
}

// ============================================================
// БАЗОВЫЙ ДЛЯ ДЕПАРТАМЕНТОВ (Departmental)
// ============================================================

public abstract class DepartmentalRoomType : RoomType
{
    public override string Category => "Departments";
}

// ============================================================
// БАЗОВЫЙ ДЛЯ АНТАГОНИСТОВ (Antags)
// ============================================================

public abstract class AntagRoomType : RoomType
{
    public override string Category => "Antags";
}

// ============================================================
// ОБЩИЕ ТИПЫ (Common)
// ============================================================

public class General : CommonRoomType
{
    public override string Name => "General";
    public override Color FillColor => Color.FromArgb(100, 220, 220, 220);
}

public class Technical : CommonRoomType
{
    public override string Name => "Technical";
    public override Color FillColor => Color.FromArgb(100, 255, 240, 200);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
}

public class BaseRoom : CommonRoomType
{
    public override string Name => "BaseRoom";
    public override bool IsHidden => true;
}

// ============================================================
// СЛУЖБА БЕЗОПАСНОСТИ (Security)
// ============================================================

public class Security : DepartmentalRoomType
{
    public override string Name => "Security";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockSecurityLocked";
    public override Color FillColor => Color.FromArgb(100, 222, 58, 58);
    public override Color LineColor => Color.FromArgb(255, 222, 58, 58);
}

public class Detective : DepartmentalRoomType
{
    public override string Name => "Detective";
    public override string DoorProto => "AirlockDetectiveLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 150, 100);
    public override Color LineColor => Color.FromArgb(255, 200, 150, 100);
}

public class Brig : DepartmentalRoomType
{
    public override string Name => "Brig";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockBrigLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 180, 50, 50);
}

public class Armory : DepartmentalRoomType
{
    public override string Name => "Armory";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "HighSecArmoryLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 30, 30);
    public override Color LineColor => Color.FromArgb(255, 150, 30, 30);
}

public class HeadOfSecurity : DepartmentalRoomType
{
    public override string Name => "HeadOfSecurity";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockHeadOfSecurityLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 40, 40);
    public override Color LineColor => Color.FromArgb(255, 200, 40, 40);
}

// ============================================================
// ИНЖЕНЕРИЯ (Engineering)
// ============================================================

public class Engineering : DepartmentalRoomType
{
    public override string Name => "Engineering";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockEngineeringLocked";
    public override Color FillColor => Color.FromArgb(100, 239, 179, 65);
    public override Color LineColor => Color.FromArgb(255, 239, 179, 65);
}

public class Atmospherics : DepartmentalRoomType
{
    public override string Name => "Atmospherics";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockAtmosphericsLocked";
    public override Color FillColor => Color.FromArgb(100, 62, 179, 136);
    public override Color LineColor => Color.FromArgb(255, 62, 179, 136);
}

public class ChiefEngineer : DepartmentalRoomType
{
    public override string Name => "ChiefEngineer";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockChiefEngineerLocked";
    public override Color FillColor => Color.FromArgb(100, 220, 160, 50);
    public override Color LineColor => Color.FromArgb(255, 220, 160, 50);
}

public class External : DepartmentalRoomType
{
    public override string Name => "External";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockExternalLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 180, 220);
    public override Color LineColor => Color.FromArgb(255, 100, 180, 220);
}

// ============================================================
// НАУКА (Science)
// ============================================================

public class Science : DepartmentalRoomType
{
    public override string Name => "Science";
    public override string DoorProto => "AirlockScienceLocked";
    public override Color FillColor => Color.FromArgb(100, 211, 129, 201);
    public override Color LineColor => Color.FromArgb(255, 211, 129, 201);
}

public class ResearchDirector : DepartmentalRoomType
{
    public override string Name => "ResearchDirector";
    public override string DoorProto => "AirlockResearchDirectorLocked";
    public override Color FillColor => Color.FromArgb(100, 190, 100, 180);
    public override Color LineColor => Color.FromArgb(255, 190, 100, 180);
}

// ============================================================
// МЕДИЦИНА (Medical)
// ============================================================

public class Medical : DepartmentalRoomType
{
    public override string Name => "Medical";
    public override string DoorProto => "AirlockMedicalLocked";
    public override Color FillColor => Color.FromArgb(100, 82, 180, 233);
    public override Color LineColor => Color.FromArgb(255, 82, 180, 233);
}

public class Virology : DepartmentalRoomType
{
    public override string Name => "Virology";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockVirologyLocked";
    public override Color FillColor => Color.FromArgb(100, 67, 153, 9);
    public override Color LineColor => Color.FromArgb(255, 67, 153, 9);
}

public class Chemistry : DepartmentalRoomType
{
    public override string Name => "Chemistry";
    public override string DoorProto => "AirlockChemistryLocked";
    public override Color FillColor => Color.FromArgb(100, 250, 117, 0);
    public override Color LineColor => Color.FromArgb(255, 250, 117, 0);
}

public class Morgue : DepartmentalRoomType
{
    public override string Name => "Morgue";
    public override string DoorProto => "AirlockMedicalMorgueLocked";
    public override Color FillColor => Color.FromArgb(100, 60, 120, 160);
    public override Color LineColor => Color.FromArgb(255, 60, 120, 160);
}

public class ChiefMedicalOfficer : DepartmentalRoomType
{
    public override string Name => "ChiefMedicalOfficer";
    public override string DoorProto => "AirlockChiefMedicalOfficerLocked";
    public override Color FillColor => Color.FromArgb(100, 70, 160, 210);
    public override Color LineColor => Color.FromArgb(255, 70, 160, 210);
}

// ============================================================
// СНАБЖЕНИЕ / КАРГО (Cargo)
// ============================================================

public class Cargo : DepartmentalRoomType
{
    public override string Name => "Cargo";
    public override string DoorProto => "AirlockCargoLocked";
    public override Color FillColor => Color.FromArgb(100, 164, 97, 6);
    public override Color LineColor => Color.FromArgb(255, 164, 97, 6);
}

public class Salvage : DepartmentalRoomType
{
    public override string Name => "Salvage";
    public override string DoorProto => "AirlockSalvageLocked";
    public override Color FillColor => Color.FromArgb(100, 141, 28, 153);
    public override Color LineColor => Color.FromArgb(255, 141, 28, 153);
}

public class Mining : DepartmentalRoomType
{
    public override string Name => "Mining";
    public override string DoorProto => "AirlockMiningLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 80, 40);
    public override Color LineColor => Color.FromArgb(255, 180, 80, 40);
}

public class Quartermaster : DepartmentalRoomType
{
    public override string Name => "Quartermaster";
    public override string DoorProto => "AirlockQuartermasterLocked";
    public override Color FillColor => Color.FromArgb(100, 150, 80, 20);
    public override Color LineColor => Color.FromArgb(255, 150, 80, 20);
}

// ============================================================
// СЕРВИС (Service)
// ============================================================

public class Service : DepartmentalRoomType
{
    public override string Name => "Service";
    public override string DoorProto => "AirlockServiceLocked";
    public override Color FillColor => Color.FromArgb(100, 159, 237, 88);
    public override Color LineColor => Color.FromArgb(255, 159, 237, 88);
}

public class Janitor : DepartmentalRoomType
{
    public override string Name => "Janitor";
    public override string DoorProto => "AirlockJanitorLocked";
    public override Color FillColor => Color.FromArgb(100, 140, 52, 127);
    public override Color LineColor => Color.FromArgb(255, 140, 52, 127);
}

public class Kitchen : DepartmentalRoomType
{
    public override string Name => "Kitchen";
    public override string DoorProto => "AirlockKitchenLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 100);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 100);
}

public class Bar : DepartmentalRoomType
{
    public override string Name => "Bar";
    public override string DoorProto => "AirlockBarLocked";
    public override Color FillColor => Color.FromArgb(100, 121, 21, 0);
    public override Color LineColor => Color.FromArgb(255, 121, 21, 0);
}

public class Hydroponics : DepartmentalRoomType
{
    public override string Name => "Hydroponics";
    public override string DoorProto => "AirlockHydroponicsLocked";
    public override Color FillColor => Color.FromArgb(100, 60, 180, 60);
    public override Color LineColor => Color.FromArgb(255, 60, 180, 60);
}

public class Chapel : DepartmentalRoomType
{
    public override string Name => "Chapel";
    public override string DoorProto => "AirlockChapelLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 180, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 180, 150);
}

public class Theatre : DepartmentalRoomType
{
    public override string Name => "Theatre";
    public override string DoorProto => "AirlockTheatreLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 100, 150);
    public override Color LineColor => Color.FromArgb(255, 200, 100, 150);
}

public class Lawyer : DepartmentalRoomType
{
    public override string Name => "Lawyer";
    public override string DoorProto => "AirlockLawyerLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 180, 200);
    public override Color LineColor => Color.FromArgb(255, 180, 180, 200);
}

// ============================================================
// КОМАНДОВАНИЕ (Command)
// ============================================================

public class Command : DepartmentalRoomType
{
    public override string Name => "Command";
    public override string DoorProto => "AirlockCommandLocked";
    public override Color FillColor => Color.FromArgb(100, 51, 77, 109);
    public override Color LineColor => Color.FromArgb(255, 51, 77, 109);
}

public class Captain : DepartmentalRoomType
{
    public override string Name => "Captain";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "HighSecCaptainLocked";
    public override Color FillColor => Color.FromArgb(100, 30, 50, 80);
    public override Color LineColor => Color.FromArgb(255, 30, 50, 80);
}

public class HeadOfPersonnel : DepartmentalRoomType
{
    public override string Name => "HeadOfPersonnel";
    public override string DoorProto => "AirlockHeadOfPersonnelLocked";
    public override Color FillColor => Color.FromArgb(100, 70, 90, 130);
    public override Color LineColor => Color.FromArgb(255, 70, 90, 130);
}

public class CentralCommand : DepartmentalRoomType
{
    public override string Name => "CentralCommand";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "HighSecCentralCommandLocked";
    public override Color FillColor => Color.FromArgb(100, 40, 60, 100);
    public override Color LineColor => Color.FromArgb(255, 40, 60, 100);
}

public class EVA : DepartmentalRoomType
{
    public override string Name => "EVA";
    public override string DoorProto => "AirlockEVALocked";
    public override Color FillColor => Color.FromArgb(100, 80, 150, 200);
    public override Color LineColor => Color.FromArgb(255, 80, 150, 200);
}

public class Vault : DepartmentalRoomType
{
    public override string Name => "Vault";
    public override string WallProto => "WallReinforced";
    public override string DoorProto => "AirlockVaultLocked";
    public override Color FillColor => Color.FromArgb(100, 180, 180, 50);
    public override Color LineColor => Color.FromArgb(255, 180, 180, 50);
}

// ============================================================
// ТЕХНИЧЕСКИЕ ТОННЕЛИ (Maintenance)
// ============================================================

public class Maintenance : DepartmentalRoomType
{
    public override string Name => "Maintenance";
    public override string DoorProto => "AirlockMaintLocked";
    public override Color FillColor => Color.FromArgb(100, 100, 100, 100);
    public override Color LineColor => Color.FromArgb(255, 100, 100, 100);
}

// ============================================================
// НЕЙТРАЛЬНЫЕ
// ============================================================

public class Neutral : DepartmentalRoomType
{
    public override string Name => "Neutral";
    public override Color FillColor => Color.FromArgb(100, 212, 212, 212);
    public override Color LineColor => Color.FromArgb(255, 212, 212, 212);
}

public class NeutralLight : DepartmentalRoomType
{
    public override string Name => "Neutral Light";
    public override Color FillColor => Color.FromArgb(180, 212, 212, 212);
    public override Color LineColor => Color.FromArgb(200, 212, 212, 212);
}

// ============================================================
// АНТАГОНИСТЫ (Antags)
// ============================================================

public class Syndicate : AntagRoomType
{
    public override string Name => "Syndicate";
    public override string DoorProto => "AirlockSyndicateLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 50, 50);
    public override Color LineColor => Color.FromArgb(255, 200, 50, 50);
}

public class Nukeop : AntagRoomType
{
    public override string Name => "Nukeop";
    public override string DoorProto => "AirlockSyndicateNukeopLocked";
    public override Color FillColor => Color.FromArgb(100, 200, 30, 30);
    public override Color LineColor => Color.FromArgb(255, 200, 30, 30);
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

    public override string Name => Data.Name;
    public override string Category => Data.Category;
    public override string WallProto => Data.WallProto;
    public override string FloorProto => Data.FloorProto;
    public override string DoorProto => Data.DoorProto;
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
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
}

public class ExportData
{
    public string Type { get; set; } = "Single";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Custom";
    public string WallProto { get; set; } = "WallSolid";
    public string FloorProto { get; set; } = "Plating";
    public string DoorProto { get; set; } = "";
    public string FillColor { get; set; } = "200,230,230,230";
    public string LineColor { get; set; } = "255,180,180,180";
}