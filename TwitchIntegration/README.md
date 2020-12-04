# HunterPie - Twitch Integration

This plugin will create a Twitch bot client, adding Monster Hunter: World related commands to your Twitch chat.

**ATTENTION: THIS PLUGIN REQUIRES HUNTERPIE v1.0.3.99 OR GREATER.**

## Installation

1. Drag'n'drop the icon below into your HunterPie window:

[<img src="https://raw.githubusercontent.com/amadare42/HunterPie.SyncPlugin/master/readme/plugin.svg">](https://raw.githubusercontent.com/Haato3o/HunterPie.Plugins/master/TwitchIntegration/module.json)

2. Restart application.

### Setting up your bot

This plugin has a `config.json` file to set your Bot information in order for it to connect to Twitch. Open it with a text editor and set the follow information accordingly:

```js
{
  "Username": "MyBotName",                  // This is your Bot username
  "OAuth": "oauth:myBotOauthToken1234",     // This is your bot OAuth token prefixed by oauth:, you can get one here: https://twitchapps.com/tmi/
  "Channel":  "Haato__"                     // This is your channel name
}
```

## Supported Commands

Here's a list of the current supported commands for this bot:

> **Note:** Commands are **case-insensitive**.

Command name | Description | Bot response
:-----------:|:--------------------------------------------------|:---------
!id,!session          | Sends your current session ID to your Twitch chat. | `Session Id: 1@a2SdxK63aW`
!build       | Sends your current build to your Twitch chat, the link is to Honey Hunters World, but it's shortened using Tinyurl. | `My current Bow build: https://tinyurl.com/y6av65mw`
!rank        | Sends your current character basic information to your Twitch chat. | `Lyss \| HR: 308 \| MR: 129 \| Playtime: 43.05:25:30`

## For more custom commands

HunterPie plugins have access to every information HunterPie tracks, see the [Plugin documentation here](https://docs.hunterpie.me/?p=Plugins/plugins.md), if you want to add more commands, you can find the source code for this plugin [here](https://github.com/Haato3o/HunterPie.Plugins/blob/master/TwitchIntegration/main.cs).

> **NOTE:** Don't forget to rename the `main.cs` to anything else after you've compiled it, otherwise HunterPie will compile it over and over every time you start it again.