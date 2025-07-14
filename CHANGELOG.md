## v2.0.1: MoCore 2.0

Just a quick update to update the mod to use MoCore 2.0, also added playerId to some events.

## v2.0.0: Rebrand!

Added the ability to do multiple outputs on an event, now can send to a http webserver as well as Streamer.bot.

Renamed project to SlipEvent to better reflect the purpose of the mod.

## v1.2.3: Game Update

Updated to support current game version of v1.90.0. And some internal changes to enable the ability to manage the compatible game versions without having to make a new release.

## v1.2.2: Custom Order event

Added a new event for receiving/sending a custom order, including who sent the order.
Verified compatible with the 1.80.0 Hotfix 1 update, released August 18th, 2024.

## v1.2.1: Game Update!

Updated to work with v1.70.0 of the game. Also added a game version checker that runs at launch to quietly disable ourselves if the game version changes. (Should trigger every time the game updates.)

## v1.2.0: Captain Config

### Additions
- Added a new configuration option to require being the Captain of the ship in order for an event to be sent to Streamer.bot. There is an option to set the default behavior and then you can override it on a per-event basis. More details in the config file after launching once.
    - Note this does not work with the following events: "GameLaunch", "GameExit", "JoinShip" (These will always be sent since captain data is not available when these are triggered.)

## v1.1.0: Crew Events

### Additions
- Three new crew-based events: [CrewmateCreated](https://github.com/MoSadie/SlipStreamer.bot?tab=readme-ov-file#crewmatecreated), [CrewmateRemoved](https://github.com/MoSadie/SlipStreamer.bot?tab=readme-ov-file#crewmateremoved), and [CrewmateSwapped](https://github.com/MoSadie/SlipStreamer.bot?tab=readme-ov-file#crewmateswapped)
- Configurable cooldowns for each event. Any events that hit this cooldown will be skipped. More details in the config file after launching once.

### Streamer.bot Action Additions
- Added actions to handle the new events.
- Added automatic predictions on run start/end.
- Added crew tracking system. Creates non-persisted global variables showing how many of each archetype are currently on the ship.

If you encounter any issues, please report them on the [Issues](https://github.com/MoSadie/SlipStreamer.bot/issues) page!

**Full Changelog**: https://github.com/MoSadie/SlipStreamer.bot/compare/v1.0.1...v1.1.0

## v1.0.1: Stability

Added a little more robust error handling. Now if anything crashes in the mod my code should handle it and the game should ignore it.

## v1.0.0: Initial Release!

Yay! It's finally here!

I'm working on a "How to install" video right now, will have that linked in the readme soon. In the meantime the readme has written instructions.

Please report any issues you find [here](https://github.com/MoSadie/SlipStreamer.bot/issues)!