# Отчёт: голопады и шкафчики по комнатам

Сопоставление `RoomType` → `HolopadProto` / `LockerProto` (дефолты в свойствах каждого типа комнаты).

## 1. Комнаты без голопада (HolopadProto = null)

| Класс комнаты | Категория |
|---|---|
| Syndicate | Antags |
| Nukeop | Antags |
| BaseRoom* | Common (скрытый) |
| Disposals | Common+ (наследует Technical) |

\* BaseRoom скрыт и не используется напрямую — можно игнорировать.

## 2. Комнаты без шкафчика (LockerProto = null)

> Почти все комнаты изначально не имеют шкафчика — это норма (шкафчик не для каждого помещения).
> Ниже только «явные» помещения, где шкафчик ожидаем, но назначение не очевидно.

| Класс комнаты | Категория |
|---|---|
| Armory | Security |
| All AI* (AI, AIMain, AIUpload, AIEntrance, AIChute, SatelliteAI) | AI |
| Morgue | Medical |
| External | Engineering |

## 3. Голопады из holopad.yml без привязки к типу комнаты

Следующие прототипы голопадов существуют в `holopad.yml`, но не используются как дефолт ни одного `RoomType` в коде:

| Прототип голопада | Назначение (из yml) |
|---|---|
| HolopadGeneralDisposals | Мусоропровод |
| HolopadGeneralEVAStorage | (EVA есть в Command/EVA) |
| HolopadGeneralTheater | (Theatre есть) |
| HolopadCommandVault | (Vault есть) |
| HolopadCommandBridgeHallway | (BridgeHallway есть) |
| HolopadMedicalCryopods | (Cryogenetics есть) |
| HolopadMedicalVirology | (Virology есть) |
| HolopadMedicalSurgery | (MedicalSurgery есть) |
| HolopadScienceAnomaly | (Anomalistics есть) |
| HolopadScienceArtifact | (Xenobiology есть) |
| HolopadScienceArtifactNorth/South | (ArtifactNorth/South есть) |
| HolopadAiCore / AiMain / AiUpload / AiEntrance / AiChute / AiBackupPower | (AI-типы есть) |
| HolopadSecurityCourtroom | (Courtroom есть) |
| HolopadCargoMailroom | (Mailroom есть) |
| HolopadEngineeringAME / Power / Telecoms / AtmosTeg / AtmosFront / TechVault | (Eng-типы есть) |
| HolopadCargoAts | без комнаты |
| HolopadCommandBridgeLongRange | без комнаты |
| HolopadCargoBayLongRange | без комнаты |
| HolopadCargoShuttle | без комнаты |
| HolopadCentCommEvacShuttle | без комнаты (CentralCommand использует его) |
| HolopadServiceClownMime | без комнаты |

**Действительно без комнаты** (не привязаны ни к одному RoomType): `HolopadCargoAts`, `HolopadCommandBridgeLongRange`, `HolopadCargoBayLongRange`, `HolopadCargoShuttle`, `HolopadServiceClownMime`, `HolopadGeneralEvac`/`HolopadGeneralTools` используются как дэфолт только через Common+, частично.

## 4. Шкафчики из Fills/Lockers без привязки к типу комнаты

Большая часть шкафчиков — специализированные версии (Hardsuit, alternate, wall, лут-таблицы), которые не привязываются к типу комнаты. Основные «базовые» шкафчики, не назначенные ни одному типу:

| Шкафчик | Назначение | Возможная комната |
|---|---|---|
| LockerWallMedicalFilled / LockerMedicineFilled | Медснабжение | MedicalStorage |
| WardrobeMedicalDoctorFilled | Врач | Medical |
| WardrobeChemistryFilled | Химик | Chemistry |
| WardrobeVirologyFilled | Вирусолог | Virology |
| WardrobeScienceFilled | Учёный | Science |
| WardrobeEngineeringFilled / LockerEngineerFilledHardsuit | Инженер | Engineering |
| WardrobeAtmosphericsFilled | Атмосфера | Atmospherics |
| WardrobeSecurityFilled | Охрана | Security |
| WardrobeCargoFilled / WardrobeSalvageFilled | Снабжение/утилизация | Cargo / Salvage |
| WardrobeBotanistFilled | Ботаник | Hydroponics |
| WardrobeChapelFilled | Священник | Chapel |
| WardrobeRoboticsFilled | Робототехник | Robotics |
| SuitStorageEVA / SuitStorageEngi / SuitStorageSec / SuitStorage* | Хранение снаряжения | EVA |

## Сводка

- Комнат всего (конкретных типов): ~112
- С голопадом: ~111 (все, кроме Syndicate/Nukeop/BaseRoom)
- С голопóдом без комнаты: 6