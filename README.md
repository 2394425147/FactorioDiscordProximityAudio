# Factorio Discord Proximity Audio

## Requirements

- A Windows machine. An external application is required to transmit data to other clients.
- Discord installed with full Discord RPC interface support (Clients like Vesktop don't work due to the usage of arRPC,
  which doesn't support updating user volume via RPC).
- (Host only) An address and port connectable through the internet. (Please exercise network security)

## Setup

### Hosting

> [!WARNING]  
> This project uses RPC features that are **invite-only**.
> If you're using a custom Discord developer application, remember to invite your friends as app testers.

Download the client from [the releases page](https://github.com/2394425147/FactorioDiscordProximityAudio/releases).

Go to [the official developer portal](https://discord.com/developers/applications) and create a Discord application.

Go to the "OAuth2" tab, and set a redirect URI. (Don't close this tab yet!)

In the same folder as the client, create a `Client.dll.config` file, and fill it with the content like this:

```
<?xml version="1.0" encoding="utf-8"?>

<configuration>
    <appSettings>
        <add key="DiscordOAuthClientId" value="YOUR OAUTH CLIENT ID" />
        <add key="DiscordOAuthClientSecret" value="YOUR OAUTH CLIENT SECRET" />

        <!-- Any redirect uri will work, such as "http://localhost/test" -->
        <add key="DiscordOAuthRedirectUri" value="http://localhost/test" />
    </appSettings>
</configuration>
```

Open the client. (You need .NET 9 to run this application)

Switch the mode to "Host", and click the "Connect" button. Once you're authorized on Discord, your
players can connect to you via IP and PORT. Host mode runs a websocket client and a websocket server at the same time.

### Joining

Download the client from [the releases page](https://github.com/2394425147/FactorioDiscordProximityAudio/releases).

In the same folder as the client, create a `Client.dll.config` file with the credentials for your Discord application.
(Your hosting friend should have made this file for you)

Open the client. (You need .NET 9 to run this application)

Set the IP and PORT to your host's address. Once you're authorized on Discord, Discord users in the
same voice channel that's also in the Factorio multiplayer game will have their volume affected.

## Building Help

### Missing DiscordIPC Reference

This project has a reference to a modified version of [DiscordIPC](https://github.com/dcdeepesh/DiscordIPC).
To fix this, clone the DiscordIPC project in the same folder as this project's .sln file.

```
FactorioDiscordProximityAudio
├─ Client
├─ DiscordIPC
└─ FactorioDiscordProximityAudio.sln
```

Then, inside `DiscordIPC/Commands/SetUserVoiceSettings.cs`, change `volume` and `left`, `right` fields to use the
`double?` type.

```csharp
public class SetUserVoiceSettings
{
    public class Args
    {
        public string  user_id { get; set; }
        public Pan     pan     { get; set; }
        public double? volume  { get; set; }
        public bool?   mute    { get; set; }
    }
    public class Data
    {
        public string  user_id { get; set; }
        public Pan     pan     { get; set; }
        public double? volume  { get; set; }
        public bool?   mute    { get; set; }
    }
    public class Pan
    {
        public double? left  { get; set; }
        public double? right { get; set; }
    }
}
```

### Discord OAuth2 Credentials

This application depends on an `App.config` file to run and properly authenticate with Discord.

This project provides an example config file named `App.example.config`. Copy or rename it to `App.config`.

Create a Discord application on Discord's [developer portal](https://discord.com/developers/applications).

Go to the "OAuth2" tab, set a redirect URI, and fill out the information in the `App.config` file accordingly.
