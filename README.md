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
- Thirteen settings sections, explicit interface language selection, and automatic Russian on Russian Windows. New interface text supports English and Russian; other existing language packs are retained with English fallback for new text. Interface language changes apply immediately to open windows.
- Chat and notification functions, including whisper popups when the game is in the background, subject to the user's event and sound settings.

Graph collection is **off by default**. Enable it in Full settings → Graph if wanted. Collection runs in the background and the graph is available in Overview; no graph expands beneath the compact meter. Data collection uses some CPU and memory, particularly during long encounters.

## Screenshots

The actual Modern Meter 1.0.3 interface, rendered with fictional player names and example combat data. No personal profiles, account details or combat logs are used.

### Compact overlay

![Compact overlay with exact DPS, a combat indicator and deaths in slot 5](https://raw.githubusercontent.com/Volframid/modern-meter/main/docs/screenshots/compact-meter.png)

### Encounter overview

![Encounter overview and individual player statistics](https://raw.githubusercontent.com/Volframid/modern-meter/main/docs/screenshots/encounter-overview.png)

<details>
<summary>Appearance controls and refresh information</summary>

![Opacity, scale and the core-managed statistics refresh](https://raw.githubusercontent.com/Volframid/modern-meter/main/docs/screenshots/appearance-controls.png)

</details>

<details>
<summary>Appearance and labeled color controls</summary>

![Clearly labeled background, border and player color controls](https://raw.githubusercontent.com/Volframid/modern-meter/main/docs/screenshots/appearance-settings.png)

</details>

<details>
<summary>Five metric slots for DPS, tanks and healers</summary>

![Slot 4 empty and slot 5 set to deaths for every role](https://raw.githubusercontent.com/Volframid/modern-meter/main/docs/screenshots/metric-layout.png)

</details>

<details>
<summary>Russian interface</summary>

![Localized Russian settings and metric choices](https://raw.githubusercontent.com/Volframid/modern-meter/main/docs/screenshots/russian-settings.png)

</details>

## Uploads and personal settings

Fresh installations have empty upload username and authentication-token fields, with uploading disabled. The Asura upload, whitelist and glyph service addresses are prefilled. Enter your own credentials to enable uploads.

Your settings are created locally on first launch and are not overwritten by module updates. Updating an existing installation preserves its configuration. Do not share resources/config, logs, combat exports or authentication tokens.

The native runtime keeps its compatible internal ShinraMeter filenames; the module and visible interface are named Modern Meter. Updates are delivered through Toolbox rather than the original native updater.

Chat and party notification bubbles and audio are shown only while TERA is in the background; combat-specific alerts retain their own event rules. Cyrillic text selects an installed Russian Windows speech voice. If that voice is unavailable, a notification sound is used.

The fifth metric slot is aligned to the right edge and defaults to deaths. On the first upgrade, legacy slot-four deaths move to slot five and slot four becomes empty, for damage dealers, tanks and healers. A local migration marker makes this a one-time change; later custom slot choices are preserved on restart. Profiles already migrated by 1.0.2 keep their subsequent choices. Overlay opacity is retained precisely in the local resources/config/modern-ui.json compatibility settings.

The legacy 1–20 Hz control is not used by the retained calculation core. The interface shows its approximately one-second statistics refresh instead; combat-timer animation is independent.

## Source and validation

Modified interface/transport source and the completed Russian satellite resources are in src/. See scripts/build.ps1 to rebuild using the runtime libraries shipped here. The calculation, protocol-data and other original runtime libraries are retained.

This release is checked with isolated interface, settings, localization, graph, notification, transport and packaging tests. Those checks do not replace gameplay testing on each server and Windows configuration.

## Credits and license

Based on [Shinra Meter](https://github.com/neowutran/ShinraMeter) and the [Asura Toolbox integration](https://github.com/myaak/shinra-meter-asura). Original contributors include Gothos, Foglio, Gl0, neowutran and others. Their MIT copyright and license are retained in LICENSE and source files. Bundled dependencies retain their respective licenses and attribution.
