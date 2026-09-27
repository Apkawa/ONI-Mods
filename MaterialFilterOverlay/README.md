Extends the built-in "Materials Overlay" (F4). When you select a material category (for example, Metal), a "+" button appears next to it; clicking it opens a list of all materials in that category. You can check one or more specific materials, and the overlay will then highlight only the buildings and floor tiles made of exactly those materials — for example, to find all gold structures, demolish them, and recover the gold. If no specific material is checked, the whole selected category is highlighted as before.

<!-- Mandatory DLC compatibility matrix, rendered as images -->
![DLC1YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc1Yes.png)
![DLC2YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc2Yes.png)
![DLC3YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc3Yes.png)
![DLC4YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc4Yes.png)
![DLC5YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc5Yes.png)
![VanillaYES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/VanillaYes.png)

# Features

* A "+" button appears next to the selected material category in the "Materials Overlay" (F4)
* Clicking it opens a list of all materials of that category
* Check one or more specific materials — the overlay then highlights only buildings and floor tiles made of exactly those materials (for example, find all gold structures to demolish them and recover the gold)
* If no specific material is checked, the whole selected category is highlighted as before
* Switching to another category closes the list and resets the sub-selection

# Changelog

* 2026-09-27: v0.0.1 Initial release
* 2026-09-27: v0.0.2 Fixed category filters not responding to clicks; the whole category row (label and checkbox) is now clickable, as in the vanilla overlay
* 2026-09-27: v0.0.3 Reworked the panel layout to match the vanilla filter menu: labels are left-aligned and all checkboxes (category and material) sit in a right column; fixed stale highlights — objects that stopped matching the filter (for example, aluminum buildings after selecting a copper-ore filter) no longer keep glowing until the camera scrolls them offscreen and back
* 2026-09-27: v0.0.4 Fixed the layout for real: rows now stretch to the full panel width, so the category names are truly left-aligned and the checkboxes sit in a right column; category filters are rendered as radio buttons (single-select) instead of checkboxes, matching the vanilla selection behaviour
* 2026-09-27: v0.0.5 Fixed the selected radio button showing an empty ring instead of a filled dot; the panel no longer overflows past the right border — it is now pinned to the width of the vanilla filter menu it replaces; added padding around the panel content on all four sides
* 2026-09-27: v0.0.6 Fixed the radio dot staying stuck on its initial position after switching categories — the checked state's sprite and color are now forced on every selection change; clicking a material name (not just its small checkbox square) now toggles that material, matching the category rows

# Source and Support

In case of problems with this mod, please open an issue on the [GitHub source code webpage](https://github.com/Apkawa/ONI-Mods). Source code for all of my mods is also located at this link.
