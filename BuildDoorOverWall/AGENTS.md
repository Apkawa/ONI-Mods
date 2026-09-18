# In-game Doors (Reference for Bug Reports)

All doors in the game: Russian name (from `strings_preinstalled_ru_klei.po`) ↔ English name
(from `STRINGS.BUILDINGS.PREFABS.*.NAME`) ↔ config class (prefab ID in parentheses).

| ru | en | class name |
| --- | --- | --- |
| Пневматическая дверь | Pneumatic Door | `DoorConfig` (`Door`) |
| Плетеная дверь | Wicker Door | `WoodenDoorConfig` (`WoodenDoor`) |
| Ручной шлюз | Manual Airlock | `ManualPressureDoorConfig` (`ManualPressureDoor`) |
| Теплоизолированная дверь | Insulated Door | `InsulatedDoorConfig` (`InsulatedDoor`) |
| Механический шлюз | Mechanized Airlock | `PressureDoorConfig` (`PressureDoor`) |
| Дверь бункера | Bunker Door | `BunkerDoorConfig` (`BunkerDoor`) |
| Внутренний люк | Interior Hatch | `ClustercraftInteriorDoorConfig` (`ClustercraftInteriorDoor`) |
| Укрепленная дверь (внешняя) | Security Door | `POIBunkerExteriorDoor` (`POIBunkerExteriorDoor`) |
| Укрепленная дверь (внутренняя) | Security Door | `POIDoorInternalConfig` (`POIDoorInternal`) |
| Двери в вестибюль | Lobby Doors | `POIFacilityDoorConfig` (`POIFacilityDoor`) |
| Выставочные двери | Showroom Doors | `POIDlc2ShowroomDoorConfig` (`POIDlc2ShowroomDoor`) |
| Дверь Gravitas | Gravitas Door | `GravitasDoorConfig` (`GravitasDoor`) |

## Details

- Strings: `STRINGS.BUILDINGS.PREFABS.<STRING_CLASS>.NAME`
  (in `lib_sources/Assembly-CSharp/STRINGS/BUILDINGS.cs`); RU translation —
  `~/ONI/game/OxygenNotIncluded_Data/StreamingAssets/strings/strings_preinstalled_ru_klei.po`.
- The shared component on almost every door is **`Door`** (enum `Door.doorType`):
  - `Internal`: `DoorConfig`, `WoodenDoorConfig`, `POIDoorInternalConfig`, `GravitasDoorConfig`
  - `ManualPressure`: `ManualPressureDoorConfig`, `InsulatedDoorConfig`, `POIFacilityDoorConfig`, `POIDlc2ShowroomDoorConfig`
  - `Pressure` (the enum's default value; never set explicitly in code): `PressureDoorConfig`, `BunkerDoorConfig`
  - `Sealed`: `POIBunkerExteriorDoor`
- **`ClustercraftInteriorDoorConfig`** is the exception: the component is not `Door`
  but `ClustercraftInteriorDoor` (the habitat's own hatch controller).
- **Build menu** (Doors category, `TUNING/BUILDINGS.cs`): only the first 6 rows of the
  table. The rest are POI-structure doors (Gravitas, Showroom, habitat) — the player
  does not build them; they appear together with the structure.
- ⚠️ **Duplicate names:** «Укрепленная дверь» / «Security Door» are **two different
  prefabs**: `POIBunkerExteriorDoor` (exterior, `Sealed`) and `POIDoorInternal`
  (interior, `Internal`). Clarify which one in bug reports.
- `ClustercraftExteriorDoor` (the habitat's exterior hatch) has no localization
  (part of the habitat structure, not in the build menu).
- `BunkerDoor` is the only prefab with the `GameTags.Bunker` tag among the
  "regular" doors (`IsFoundation = true`, same as `PressureDoor`).
- DLC: `WoodenDoor` → `DLC2`, `ClustercraftInteriorDoor` → `EXPANSION1`,
  the rest — base game / POI.
