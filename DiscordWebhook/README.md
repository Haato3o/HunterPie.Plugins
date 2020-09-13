# HunterPie - Discord Webhook

This plugin submits data in Monster Hunter: World to specified Discord channel.

## Installation

1. Download the zipped plugin [here](https://cdn.discordapp.com/attachments/652762250746265600/754689770507993148/DiscordWebhook.zip);
2. Extract it's contents to `HunterPie/Modules`;
3. Edit the `HunterPie/Modules/DiscordWebhook/config.json` and set your webhook link. Read [Setting up Webhook](#Setting-up-Webhook) if you need help.
4. Run HunterPie.

### Setting up Webhook

> **Note:** It's better to create webhook for each individual, the webhook name could be specified to the username. So that we can identify who is using this webhook to post data.

This plugin has a `config.json` file to set your Discord webhook. Open it with a text editor and set the follow information accordingly:

```js
{
  "Webhook": "https://discordapp.com/api/webhooks/12345/ooxx"          // This is your Discord webhook link
}
```

## Supported Hotkey

Here's a list of the current supported hotkeys:

Hotkey | Description
:-----------|:--------------------------
shift+ctrl+p | Sends DPS data from latest combat to Discord channel.
shift+ctrl+g | Sends your current build to Discord channel, the link is to Honey Hunters World, but it's shortened using Tinyurl.

## For more custom commands

HunterPie plugins have access to every information HunterPie tracks, see the [Plugin documentation here](https://docs.hunterpie.me/?p=Plugins/plugins.md), if you want to add more commands, you can find the source code for this plugin [here](https://github.com/acelan/HunterPie.Plugins/blob/master/DiscordWebhook/main.cs).

> **NOTE:** Don't forget to rename the `main.cs` to anything else after you've compiled it, otherwise HunterPie will compile it over and over every time you start it again.
