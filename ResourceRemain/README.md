Adds quick per-cycle resource estimates to the built-in resource screens: hover a resource to see how it changes per cycle, how long the current supply will last, and how many cycles of food supply remain.

<!-- Mandatory DLC compatibility matrix, rendered as images -->
![DLC1YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc1Yes.png)
![DLC2YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc2Yes.png)
![DLC3YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc3Yes.png)
![DLC4YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc4Yes.png)
![DLC5YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc5Yes.png)
![VanillaYES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/VanillaYes.png)

# Features

* Hovering a resource row in the right-side pinned resources panel or the "Show all resources" screen shows two lines: the current (in progress) cycle and the previous (completed) cycle. Each line shows the net change with consumed/produced breakdown (e.g. `-800 units (-1000 units +200 units)`) and, when the resource is running out, an approximate number of cycles the supply will last.
* The calorie counter tooltip now shows how many cycles the current food supply will last overall and for each food individually, taking into account how many duplicants are alive and which foods are forbidden to them (foods nobody can eat are marked `(-)`).
* Localized into English and Russian.

# Limitations

* Estimates are approximate: production and consumption can vary, and the current-cycle figures are normalized over the part of the cycle that has already elapsed, so they may shift until the cycle ends.
* The remaining-cycles number is not shown when it exceeds 999 cycles or when the per-cycle change is negligible.
* The calorie estimates use the minimum calorie burn per duplicant for the current game settings; duplicant traits can make them burn more.

# Changelog

* 2026-09-25: 0.0.1 Initial release.

# Source and Support

In case of problems with this mod, please open an issue on the [GitHub source code webpage](https://github.com/Apkawa/ONI-Mods). Source code for all of my mods is also located at this link.
