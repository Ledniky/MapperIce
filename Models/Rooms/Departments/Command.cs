using System.Drawing;

namespace MapperIce.Models;

// ============================================================
// БАЗОВЫЙ ДЛЯ КОМАНДОВАНИЯ
// ============================================================

public abstract class CommandRoomType : RoomType
{
    public override string Category => "Command";
    public override string FloorProto => "FloorSteel";
    public override string WallProto => "WallReinforced";
    
    public override Color FillColor => Color.FromArgb(100, 30, 50, 100);
    public override Color LineColor => Color.FromArgb(255, 51, 77, 109);
}

// ============================================================
// КОМНАТЫ КОМАНДОВАНИЯ
// ============================================================

public class Command : CommandRoomType
{
    public override string Name => "Command";
    public override string DisplayName => "Командование";
    public override string Description => "Комната командования. Основной командный отсек для координации действий экипажа.";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override string? HolopadProto => "HolopadCommandBridge";
    public override int Priority => 150;
}

public class EVA : CommandRoomType
{
    public override string Name => "EVA";
    public override string DisplayName => "ЕВА (ВКД)";
    public override string Description => "Хранилище скафандров";
    public override string DoorProto => "AirlockEVALocked";
    public override string GlassDoorProto => "AirlockEVAGlassLocked";
    public override string? HolopadProto => "HolopadGeneralEVAStorage";
    public override string? SuitStorageProto => "SuitStorageEVA";
    public override int Priority => 140;
}

public class Vault : CommandRoomType
{
    public override string Name => "Vault";
    public override string DisplayName => "Хранилище";
    public override string Description => "Хранилище. Укреплённое помещение с высокосекьюрными дверями для хранения ценных ресурсов.";
    public override string DoorProto => "HighSecCommandLocked";
    public override string GlassDoorProto => "HighSecCommandLocked";
    public override string? HolopadProto => "HolopadCommandVault";
    public override string? LockerProto => "LockerFreezerVaultFilled";
    // Двери хранилища должны выигрывать у обычных комнат командования
    // (Command/Bridge/CommandLounge — 150), иначе на общей стене остаётся
    // AirlockCommandLocked вместо HighSecCommandLocked
    public override int Priority => 200;
}

public class Captain : CommandRoomType
{
    public override string Name => "Captain";
    public override string? FaxName => "Кабинет капитана";
    // Как на эталонных картах: у рубки капитана отдельный прототип факса
    public override string FaxProto => "FaxMachineCaptain";
    public override string DisplayName => "Капитан";
    public override string Description => "Капитанская каюта. Личные покои капитана станции с максимальной защитой.";
    public override string DoorProto => "AirlockCaptainLocked";
    public override string GlassDoorProto => "AirlockCaptainGlassLocked";
    public override string? HolopadProto => "HolopadCommandCaptain";
    public override string? LockerProto => "LockerCaptainFilledNoLaser";
    public override string? SuitStorageProto => "SuitStorageCaptain";
    public override string? BedProto => "Bed";
    public override string? BedsheetProto => "BedsheetCaptain";
    public override string? DresserProto => "DresserCaptainFilled";
    public override int Priority => 300;
}

public class HeadOfPersonnel : CommandRoomType
{
    public override string Name => "HeadOfPersonnel";
    public override string? FaxName => "Кабинет главы персонала";
    public override string DisplayName => "Глава персоонала";
    public override string Description => "Кабинет начальника кадров. Офис руководителя отдела кадров и управления персоналом.";
    public override string DoorProto => "AirlockHeadOfPersonnelLocked";
    public override string GlassDoorProto => "AirlockHeadOfPersonnelGlassLocked";
    public override string? HolopadProto => "HolopadCommandHop";
    public override string? LockerProto => "LockerHeadOfPersonnelFilled";
    public override string? BedProto => "Bed";
    public override string? BedsheetProto => "BedsheetHOP";
    public override string? DresserProto => "DresserHeadOfPersonnelFilled";
    public override int Priority => 250;
}

public class Bridge : CommandRoomType
{
    public override string Name => "Bridge";
    public override string? FaxName => "Мостик";
    public override string DisplayName => "Мостик";
    public override string Description => "Мостик. Центральная командная точка управления станцией с полным обзором.";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override string? HolopadProto => "HolopadCommandBridge";
    public override int Priority => 150;
}

public class CommandLounge : CommandRoomType
{
    public override string Name => "CommandLounge";
    public override string DisplayName => "Зал совещаний";
    public override string Description => "Зал совещаний. Командная комната для проведения встреч и переговоров.";
    public override string DoorProto => "AirlockCommandLocked";
    public override string GlassDoorProto => "AirlockCommandGlassLocked";
    public override string? HolopadProto => "HolopadCommandLounge";
    public override int Priority => 150;
}
