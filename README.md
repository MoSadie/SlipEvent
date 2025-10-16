# SlipEvent

Trigger external software from in-game events!

You can enable events being sent to any/all of the following:
- Streamer.bot
- HTTP Server (via POST request)

## Requirements

- [Slipstream: Rogue Space (on Steam)](https://playslipstream.com)
- [r2modman](https://thunderstore.io/c/slipstream-rogue-space/p/ebkr/r2modman/)

### Optional

- [Streamer.bot](https://streamer.bot)

## (Quick) Setup Video

https://youtu.be/AhMN6R5JOh0

## Game Installation

1) Launch Slipstream at least once.
2) Download and setup r2modman from [here](https://thunderstore.io/c/slipstream-rogue-space/p/ebkr/r2modman/) (Click "Manual Download" and run the setup exe)
3) Select Slipstream: Rogue Space from the list of games in r2modman and create a profile.
4) In the "Online" tab look for SlipStreamer.bot and click it. Then click "Download"
5) Launch Slipstream using the "Start modded" button to generate the config file.
5) Modify the config file using the "Config editor" tab to match your setup. (see the config file for more details)

## HTTP Post Request Requirements

1) Set the URL in the config file to the server you want to send the requests to.
2) Make sure the HTTP request integration is enabled in the config file.

## Streamer.bot Requirements

1) Follow the [Streamer.bot setup instructions](https://docs.streamer.bot/get-started/installation)
2) In the "Servers/Clients" tab, under HTTP Server check "Auto Start" and click "Start Server" (making note of the IP and port)
3) (Optional, but recommended) Click "Import" and paste the contents of [ImportSlipStreamerBot.txt](https://raw.githubusercontent.com/MoSadie/SlipStreamer.bot/main/ImportSlipStreamerBot.txt) in the "Input String" text box and click "Import" This provices a set of premade actions for you to use. There is a dedicated action for each event type, as well as a few fun premade features such as chat polling and automatic predictions.
4) Make sure in the SlipEvent config file that Streamer.bot integration is enabled.

**If you run into any issues getting this set up, please reach out! Best way is via Discord or GitHub Issues!**

## Events

### GameLaunch

Sent when the game is launched.

Arguments:

- eventType: "GameLaunch"


### GameExit

Sent when the game is exited.

Arguments:

- eventType: "GameExit"

### JoinShip

Sent when a ship is joined.

Arguments:

- eventType: "JoinShip"

### StartFight

Sent when a fight is started.

Arguments:

- eventType: "StartFight"
- enemy: Name of the enemy ship.
- invaders: Types of invading slugs.
- intel: Available intel on the enemy ship.
- threatLevel: Enemy threat level.
- speedLevel: Enemy speed level.
- cargoLevel: Enemy cargo level.

### EndFight

Sent when a fight is ended.

Arguments:

- eventType: "EndFight"
- outcome: "NoneYet", "CaptainVictory", "CaptainDefeat", or "CaptainRetreat"

### NodeChange

Sent when the ship moves to a new node.

Arguments:

- eventType: "NodeChange"
- isBacktrack - "True" or "False" if a backtrack.
- scenarioKey - Unique identifier for the node's type scenario.
- visited: "True" or "False" if the node has been visited before.
- completed: "True" or "False" if the node has been completed before.
- captainVictory: "True" or "False"

### ChoiceAvailable

Sent when a choice is available.

Arguments:

- eventType: "ChoiceAvailable"
- isBacktrack - "True" or "False" if a backtrack.
- scenarioKey - Unique identifier for the node's type scenario.
- visited: "True" or "False" if the node has been visited before.
- completed: "True" or "False" if the node has been completed before.
- captainVictory: "True" or "False"
- scenarioName: Name of the encounter
- scenarioDescription: Description of the encounter
- proposition: The question posed by the encounter
- choice1: The first choice
- choice2: The second choice

### KnockedOut

Sent when you are knocked out.

Arguments:

- eventType: "KnockedOut"
- message: The message displayed when knocked out. Includes color information so will need parsing.

### RunStarted

Sent when a run is started.

Arguments:

- eventType: "RunStarted"
- campaign: Counts the number of campaigns this ship has been on. (Starts at 0)
- region: The region the ship is headed to.

### RunFailed

Sent when a run is failed for any reason.

Arguments:

- eventType: "RunFailed"

### RunSucceeded

Sent when a run is succeeded.

Arguments:

- eventType: "RunSucceeded"

### NextSector

Sent when the ship moves to a new sector.

Arguments:

- eventType: "NextSector"
- sectorIndex: The index of the new sector.
- sectorName: The name of the new sector.

### CrewmateCreated

Sent when a crewmate joins the ship. Sent even if you join a ship with existing crewmates.

Arguments:

- eventType: "CrewmateCreated"
- name: The name of the crewmate. Usually their Twitch name, but they may be an Apple user.
- id: A unique identifier for the crewmate.
- level: Current level.
- xp: Total XP earned, does not reset with level up.
- archetype: The crewmate's archetype. ("cat", "hamster", "octopus", "turtle", "croc", "bear")
- statHealth: Maximum health.
- statShield: Maximum shield.

### CrewmateRemoved

Sent when a crewmate leaves the ship.

Arguments:

- eventType: "CrewmateRemoved"
- name: The name of the crewmate. Usually their Twitch name, but they may be an Apple user.
- id: A unique identifier for the crewmate.

### CrewmateSwapped

Sent when a crewmate uses the Transporter station to swap archetypes.

Arguments:

- eventType: "CrewmateSwapped"
- name: The name of the crewmate. Usually their Twitch name, but they may be an Apple user.
- id: A unique identifier for the crewmate.
- level: Current level.
- xp: Total XP earned, does not reset with level up.
- archetype: The crewmate's archetype. ("cat", "hamster", "octopus", "turtle", "croc", "bear")
- statHealth: Maximum health.
- statShield: Maximum shield.

### CustomOrder

Sent when a custom order from the helm is sent/recieved.

Arguments:

- message: The message sent from the helm.
- senderDisplayName: The display name of the player who sent the message.
- senderProfileImage: The URL for the player's profile image, or null if they do not have one.
- senderIsCaptain: "True" if the sender is the captain, "False" otherwise.

### ShipTechUpdated

Sent when the ship's tech is updated. This can be either buying new tech, upgrading existing tech, resetting after a run, or joining a ship.

Arguments:

| Name | Description |
| - | - |
| `ssb_ActiveTechs` | List of the names of active techs. |
| `ssb_AllTechs` | List of the names of all techs. |
| `ssb_<tech name>_IsActive` | Lists if a given tech is active or not. |
| `ssb_<tech name>_ShortDesc` | Gives a short description of the tech. |
| `ssb_<tech name>_LongDesc` | Gives a long description of the tech. |
| `ssb_<tech name>_Level` | Current level of the tech, 0 if not active. |
| `ssb_<tech name>_MaxLevel` | Max level of the tech. |
| `ssb_<tech name>_Color` | Hex color for the tech. |
| `ssb_<tech name>_Unit` | Unit used at the end of the value, either `VALUE`, `PERCENT`, or `SECONDS`. |
| `ssb_<tech name>_Level_<level number>_Value` | Numeric value of that level. |
| `ssb_<tech name>_Level_<level number>_Cost` | Cost in gems of that level. |