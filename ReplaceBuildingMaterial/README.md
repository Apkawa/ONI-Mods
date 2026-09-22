Allows placing the same building exactly on its own position with a different material, without demolishing it first. The game's native building replacement logic does the rest: when construction finishes, the old building is dismantled, its materials are returned to the robot, and the new one is built on the freed cell.

<!-- Mandatory DLC compatibility matrix, rendered as images -->
![DLC1YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc1Yes.png)
![DLC2YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc2Yes.png)
![DLC3YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc3Yes.png)
![DLC4YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc4Yes.png)
![DLC5YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/Dlc5Yes.png)
![VanillaYES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/VanillaYes.png)

# Features

* **Building over itself (different material)**: select the same building that is already built in the build menu and drag it exactly onto its position — the ghost highlights **white**, and a click creates a replacement plan. Verified buildings: pumps (liquid/gas), generators (coal, gas, oil), batteries, transformer, auto planter, thermostat, smelter.
* **Multi-cell buildings** work the same way — as long as the anchor cell matches (see Limitations).
* **Safe to cancel**: cancelling the plan does not break the old building (it keeps working without "overlapping ports" errors).

# Limitations

* Only "plain" buildings: defs without their own native replacement semantics (doors, foundations, stairs, windows and the like are excluded).
* Exact position match only: no rotations or offsets; a drag with an offset is rejected (the click simply does nothing).
* The same material is not replaced: copper over copper shows a red ghost ("occupied") and nothing happens.
* If the old building is being deconstructed at the moment of replacement, the game's stock behavior applies: the deconstruction is cancelled.
* Walls, wires, pipes, foundations and other non-buildings are out of scope (doors have a separate mod, [BuildDoorOverWall](../BuildDoorOverWall/README.md)).

# Changelog

* 2026-09-22: initial Same building over itself with a different material, via the game's native replacement logic.

# Source and Support

In case of problems with this mod, please open an issue on the [GitHub source code webpage](https://github.com/Apkawa/ONI-Mods). Source code for all of my mods is also located at this link.
