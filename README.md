# SilklessCoop

V2.3 - Added Player Color Indicators & Enhanced Visual Features

V2.2 - Added Menu UI

V2.1 - Added Compass sync

V2.0 - Added Steam support

V1.0 - Initial release

A simple coop mod allowing you to see your friends in each others game.

It currently features two modes:

- Steam P2P (all players need the steam version)
- Standalone (requires one player to set up a server)

## ✨ New Features (V2.3)

- **Player Color Indicators**: Colored pins above each player for easy identification
- **Local Player Debug Indicator**: White pin for your own character (debug mode only)
- **Enhanced Visual Settings**: Configurable opacity and visual options
- **Improved Synchronization**: Better compass and position sync performance

View the Nexusmods page [here](https://www.nexusmods.com/hollowknightsilksong/mods/73).

<details>
<summary>

## Screenshots / Videos

</summary>

[![Movement Footage](https://img.youtube.com/vi/CJR4MXvXHsI/0.jpg)](https://www.youtube.com/watch?v=CJR4MXvXHsI)

[![Combat Footage](https://img.youtube.com/vi/L90_3az_o0M/0.jpg)](https://www.youtube.com/watch?v=L90_3az_o0M)

![Bellhart Screenshot 1](./Media/bellhart_1.jpg)
![Bellhart Screenshot 2](./Media/bellhart_2.jpg)
![Bellhart Screenshot 3](./Media/bellhart_3.jpg)
![Bellhart Screenshot 4](./Media/bellhart_4.jpg)
![Bellhart Screenshot 5](./Media/bellhart_5.jpg)
![Shellwood Screenshot 1](./Media/shellwood_1.jpg)
![Shellwood Screenshot 1](./Media/shellwood_2.jpg)
![Shellwood Screenshot 1](./Media/shellwood_3.jpg)
![Shellwood Screenshot 1](./Media/shellwood_4.jpg)

Note: 
- Player counter in the bottom left corner when viewing the quick map (holding L1)
- Colored pins above players help identify each cooperator
- Compass icons show other players' positions on the map when they have it open

</details>

## Installation

- Download BepInEx 5 (tested on 5.4.23.3) and extract it into your root game folder
- Download SilklessCoop.zip and extract it into your root game folder
- Launch the game once to generate configuration files
- Configure settings in `BepInEx/config/SilklessCoop.cfg` if needed

## Configuration

The mod creates a configuration file at `BepInEx/config/SilklessCoop.cfg` with these options:

### General Settings
- **Toggle Key**: Key to enable/disable multiplayer (default: F5)
- **Connection Type**: Steam P2P or Standalone server
- **Tick Rate**: Update frequency (default: 20 Hz)
- **Sync Compasses**: Show other players on map (default: enabled)
- **Print Debug Output**: Enable debug logging (default: disabled)

### Visual Settings
- **Player Opacity**: Transparency of other players (default: 0.7)
- **Show Player Color Pins**: Colored indicators above players (default: enabled)
- **Active/Inactive Compass Opacity**: Map icon transparency settings

### Server Settings (Standalone mode)
- **Server IP Address**: IP of the standalone server
- **Server Port**: Port for connection

## Known bugs

- Some attacks have weird animations
- Disconnecting and reconnecting will cause issues, fixed by everyone disconnecting and reconnecting together

## Controls

- **F5**: Toggle multiplayer on/off (configurable)
- **Map View**: See other players' compass positions when they have map open
- **Visual Indicators**: Colored pins automatically appear above connected players

## What's Next

- Bugfixes and performance improvements
- Sound synchronization
- Directional arrows to other players
- Enhanced visual effects
- Public servers hosted by the community
