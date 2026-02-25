
# DV_LevelCrossings

Bring life and realism to Derail Valley's railway crossings!

**DV_LevelCrossings** transforms static railway crossing barriers into
fully dynamic, train-reactive systems. Barriers automatically lower as
trains approach and raise once the track is clear, complete with
flashing lights and warning bells.

Designed to be lightweight, reliable, and game-safe.

------------------------------------------------------------------------

## Features

-   Converts static railway crossing barriers into dynamic crossings
-   Direction-aware trigger system
-   Automatic barrier lowering on train approach
-   Automatic raising after train clears the crossing
-   Raise delay timing for realism
-   Reverse fail-safe timer (prevents crossings staying down if train
    reverses)
-   Flashing red warning lights
-   3D spatial bell audio

------------------------------------------------------------------------

## How It Works

The system is designed to behave like a real-world level crossing:

1.  Drive train towards level crossing.
2.  Once near to the crossing, Barriers lower and flashing lights and bells begin.
3.  Once train clears crossing, Barriers raise and flashing lights and bells cease.
4.  If train remains within the crossing block, then  Barriers remain down.

------------------------------------------------------------------------

## Notes

DV_LevelCrossings is a FREE mod for Derail Valley users.

If you get enjoyment or value from it, please consider buying me a
coffee:\
https://buymeacoffee.com/GrandpaPlayingGames

This helps Grandpa, a retiree living in the Philippines, continue
developing and maintaining mods for the Derail Valley community.

------------------------------------------------------------------------

## Requirements

-   Derail Valley (Steam)
-   Unity Mod Manager installed and working
-   .NET Framework as required by Derail Valley / UMM

------------------------------------------------------------------------

## Installation

### Recommended (Players)

Download the latest compiled release from Nexus Mods and follow the
installation instructions there.

------------------------------------------------------------------------

### From GitHub (Developers)

If you prefer to build DV_LevelCrossings from source:

1.  Clone this repository
2.  Open `DV_LevelCrossings.sln` in Visual Studio
3.  Build in **Release**
4.  Copy the output DLL and required files into:

```{=html}
<!-- -->
```
    Derail Valley/Mods/DV_LevelCrossings/

Required files:

-   DV_LevelCrossings.dll
-   info.json
-   Assets/ (if present)

Launch the game and enable DV_LevelCrossings in Unity Mod Manager.

------------------------------------------------------------------------

## Troubleshooting

**Mod does not appear in UMM**

-   Confirm the mod folder is directly under: Derail
    Valley/Mods/DV_LevelCrossings/
-   Confirm `info.json` is present next to the DLL.
-   Confirm you built in Release configuration.

------------------------------------------------------------------------

## Development

This repository contains the full runtime source code for
DV_LevelCrossings.

Authoring tools used to generate crossings are excluded from this public
repository.

------------------------------------------------------------------------

## Build

-   Open `DV_LevelCrossings.sln`
-   Build configuration: **Release**
-   Output DLL will be produced under: DV_LevelCrossings/bin/Release/

------------------------------------------------------------------------

## License

This project is licensed under the MIT License.

See: LICENSE

------------------------------------------------------------------------

## Credits

Created by GrandpaPlayingGames.

Derail Valley is developed by Altfuture.\
This mod is an unofficial community project.

------------------------------------------------------------------------

## Links

BuyMeACoffee:\
https://buymeacoffee.com/grandpaplayinggames

GitHub repository:\
https://github.com/GrandpaPlayingGames/DV_LevelCrossings

Nexus Mods:\
(Add link once published)

YouTube:\
https://www.youtube.com/@GrandpaPlayingGames
