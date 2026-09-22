Extends the game's building replacement mechanism: you can place one built building on top of another without manually demolishing it — including the reverse direction, walls and floor tiles over doors.

<!-- Mandatory DLC compatibility matrix, rendered as images -->
![DLC1YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc1Yes.png)
![DLC2YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc2Yes.png)
![DLC3YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc3Yes.png)
![DLC4YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc4Yes.png)
![DLC5YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc5Yes.png)
![VanillaYES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/VanillaYes.png)

# Features

* **Door over wall** (the original feature): drag a door onto a wall cell — the wall is destroyed and the door is placed over it.
* **Wall over door** (reverse direction): drag a wall building (outer wall / drywall, glass outer wall, insulation block) onto an installed door — the door is demolished, materials are returned to the robot, and the panel is placed in the drop cell. The door's second cell stays empty.
* **Floor tile over door**: the same works for floor tiles — cage, metal cage, insulation tile and other floor tiles: the tile is placed in the drop cell, the door is demolished, and materials are returned.
* **Door over door (different types)**: for example, manual airlocks over regular doors — the old door is destroyed, materials are returned, and the new door is built.

# Limitations

* A door of the same type is not replaced (same `PrefabID`, even with a different material) — intentionally, that is a separate mod.
* Does not work in sandbox / instant building (the preview shows white, but nothing gets built).
* When the door's top end is placed into a wall, the door becomes a mirrored Airlock (with ports) — an orientation quirk.
* One panel/tile per door, in one cell; the door's second cell stays empty.
* Sealed POI doors (for example, in Gravitas sectors) cannot be replaced.

# Changelog

* 2026-09-22: initial Doors over walls; walls/floor tiles over doors; different-type door over door.

# Source and Support

In case of problems with this mod, please open an issue on the [GitHub source code webpage](https://github.com/Apkawa/ONI-Mods). Source code for all of my mods is also located at this link.
