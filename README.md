# Modern Meter

A compact combat meter for TERA Toolbox, with an updated interface based on Shinra Meter.

## Install

1. Install the Microsoft .NET 7 Desktop Runtime (x64) if it is not already available.
2. Download the latest Modern Meter release, and extract the modern-meter folder into Toolbox/mods.
3. Use one meter integration at a time: disable the old Shinra Meter module before enabling Modern Meter.
4. Start Toolbox, then log into your character. Toolbox installs the external-interface dependency automatically.

For module-manager installation, use the module descriptor at https://raw.githubusercontent.com/Volframid/modern-meter/main/module.json.

## Features

- Compact player and party DPS with full integers, aligned configurable metrics, critical/healing rates and death counts.
- Original game class and skill icons, customizable role colors, transparency and scale.
- A smooth centisecond clock with an indicator for combat status.
- Overview with encounter history, player statistics and recorded graphs; right-click a player for original skill, buff and healing details.
- Thirteen settings sections, explicit interface language selection, and automatic Russian on Russian Windows. New interface text supports English and Russian; other existing language packs are retained with English fallback for new text. Restart the meter after changing interface language.
- Chat and notification functions, including whisper popups when the game is in the background, subject to the user's event and sound settings.

Graph collection is **off by default**. Enable it in Full settings → Graph if wanted. Collection runs in the background and the graph is available in Overview; no graph expands beneath the compact meter. Data collection uses some CPU and memory, particularly during long encounters.

## Uploads and personal settings

Fresh installations have empty upload username and authentication-token fields, with uploading disabled. The Asura upload, whitelist and glyph service addresses are prefilled. Enter your own credentials to enable uploads.

Your settings are created locally on first launch and are not overwritten by module updates. Updating an existing installation preserves its configuration. Do not share resources/config, logs, combat exports or authentication tokens.

The native runtime keeps its compatible internal ShinraMeter filenames; the module and visible interface are named Modern Meter. Updates are delivered through Toolbox rather than the original native updater.

## Source and validation

Modified interface/transport source and the completed Russian satellite resources are in src/. See scripts/build.ps1 to rebuild using the runtime libraries shipped here. The calculation, protocol-data and other original runtime libraries are retained.

This release is checked with isolated interface, settings, localization, graph, notification, transport and packaging tests. Those checks do not replace gameplay testing on each server and Windows configuration.

## Credits and license

Based on [Shinra Meter](https://github.com/neowutran/ShinraMeter) and the [Asura Toolbox integration](https://github.com/myaak/shinra-meter-asura). Original contributors include Gothos, Foglio, Gl0, neowutran and others. Their MIT copyright and license are retained in LICENSE and source files. Bundled dependencies retain their respective licenses and attribution.
