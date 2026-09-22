---
name: mod-readme
description: Rules and fixed template for authoring Oxygen Not Included mod README.md files.

disable-model-invocation: false
user-invocable: true
---

## Purpose

Standardizes the `README.md` for mods in this repository: fixed content rules and a fixed template.

## Scope

Applies when creating or updating a mod `README.md`.

## Authoring rules

- The README is written entirely in English.
- Keep the text short; no technical implementation details.
- The audience is gamers (players), not developers.
- No emoji.

## Template

Fill in the template below. Placeholders are given in angle brackets.

```md
<One-paragraph short description of the mod>

<!-- Mandatory DLC compatibility matrix, rendered as images -->
![DLC1YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc1Yes.png)
![DLC2YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc2Yes.png)
![DLC3YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc3Yes.png)
![DLC4YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc4Yes.png)
![DLC5YES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/Dlc5Yes.png)
![VanillaYES](https://raw.githubusercontent.com/Apkawa/ONI-Mods/master/docs/assets/VanillaYes.png)

# Features

* <list of features>

# Options

<Options section, if any>

# Limitations

<Limitations, briefly, if any>

# Changelog

* <date in 2026-09-22 format (`date -Idate`)>: <version> <one-line summary of changes>

# Source and Support

In case of problems with this mod, please open an issue on the [GitHub source code webpage](https://github.com/Apkawa/ONI-Mods). Source code for all of my mods is also located at this link.
```

## Template notes

- The DLC compatibility matrix (the six images) is mandatory and must always be kept.
- `# Options` and `# Limitations` are optional: if the section has no content, omit it together with its heading.
- Each changelog entry is a single line: `* <date>: <version> <summary>`, where the date uses the `YYYY-MM-DD` format produced by `date -Idate`.
