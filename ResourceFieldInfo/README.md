While you hold Ctrl and hover the mouse over a material cell in the world (with no tool selected), the cell's tooltip gets two extra lines describing the whole deposit of that material under the cursor: the number of cells in the deposit (e.g. `Tiles: 100`) and the total mass of all those cells (e.g. `Total: 100t`).

<!-- Mandatory DLC compatibility matrix, rendered as images -->
![DLC1YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc1Yes.png)
![DLC2YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc2Yes.png)
![DLC3YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc3Yes.png)
![DLC4YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc4Yes.png)
![DLC5YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc5Yes.png)
![VanillaYES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/VanillaYes.png)

# Features

* Adds two lines to the material cell tooltip: the number of cells in the contiguous deposit under the cursor (e.g. `Tiles: 100`) and the total mass of all those cells (e.g. `Total: 100t`).
* Works for any element: water, magma, natural solids and gases — handy for estimating how much rock a magma basin will leave, how much water a pool holds, or how much gas is compressed into a gas storage.
* The deposit includes all cells connected to the hovered one, both side-by-side and diagonally.
* Reuses the game's own translated strings, so the lines appear in whatever language your game runs in.

# Limitations

* Only works with the default (select) tool — no building, digging or manning tool selected — and only while Ctrl is held.
* The shown numbers are a snapshot at the moment of hover and refresh only when you move the cursor.
* The lines appear only where the game itself shows the material tooltip: not over buildings' tooltips and not in overlay modes that hide it.

# Changelog

* 2026-09-26: 0.0.1 Initial release.

# Source and Support

In case of problems with this mod, please open an issue on the [GitHub source code webpage](https://github.com/Apkawa/ONI-Mods). Source code for all of my mods is also located at this link.
