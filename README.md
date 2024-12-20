# AudioPlayer

**AudioPlayer** is a Unity-based plugin that provides advanced capabilities for managing and playing audio clips, complete with support for spatial audio and multiple speakers. This plugin is designed for use in SCP: SL Dedicated servers.

---

## Features

- Create, manage, and play multiple audio clips.
- Spatial audio support for immersive sound experiences.
- Add multiple speakers with individual configurations.
- Seamless integration with game events and player-specific audio setups.

---

## Installation

1. Download latest ``dependencies.zip`` from ``Releases``.
2. Extract all files and put in your dependencies folder. ( this path may be different depending on which plugin framework you use )

---

## Example Usage

Below are some example implementations of the AudioPlayer plugin in a server environment.

### Plugin Entry Point

```csharp
[PluginEntryPoint("AudioPlayer Test", "1.0.0", "Template AP test.", "Killers0992")]
public void Entry()
{
    EventManager.RegisterAllEvents(this);

    // Preload audio clips to be used by the plugin.
    AudioClipStorage.LoadClip("C:\\Users\\Kille\\Documents\\Serwer\\audio3.ogg", "audio3");
    AudioClipStorage.LoadClip("C:\\Users\\Kille\\Documents\\Serwer\\com.ogg", "shot");
}
```

### Handling Player Join Events

When a player joins, an audio player and speaker are dynamically created and attached to the player.

```csharp
[PluginEvent]
void OnJoin(PlayerJoinedEvent e)
{
    // Create an AudioPlayer for the player.
    AudioPlayer player = AudioPlayer.Create($"Player {e.Player.Nickname}");
    player.transform.parent = e.Player.GameObject.transform;

    // Add a speaker for spatial audio and attach it to the player.
    Speaker speaker = player.AddSpeaker("Player specific");
    speaker.transform.parent = e.Player.GameObject.transform;

    // Store the AudioPlayer in the player's temporary data.
    e.Player.TemporaryData.StoredData.Add("ap", player);
}
```

### Reacting to Player Actions

For example, playing a sound when a player toggles their flashlight:

```csharp
[PluginEvent]
void OnToggle(PlayerToggleFlashlightEvent ev)
{
    if (ev.Player.TemporaryData.StoredData.TryGetValue("ap", out object apo))
    {
        AudioPlayer ap = (AudioPlayer)apo;
        ap.AddClip("shot"); // Add and play the "shot" audio clip.
    }
}
```

---

## API Overview

### `AudioPlayer` Class

#### Static Methods

- `AudioPlayer.Create(string name)`: Creates a new `AudioPlayer` instance with the specified name.

#### Properties

- `Name`: The name of the `AudioPlayer` instance.
- `ControllerID`: The ID of the associated controller.

#### Methods

- `AddClip(string clipName, float volume = 1f, bool loop = false, bool destroyOnEnd = true)`: Adds a new audio clip to the player.
- `AddSpeaker(string name, ...)`: Adds a new speaker to the player.
- `RemoveSpeaker(string name)`: Removes a speaker by name.

### `Speaker` Class

Use `Speaker` instances for spatial and directed audio. Methods include creation and positioning.

---

## Usage Notes

- Always preload audio clips using `AudioClipStorage.LoadClip(path, alias)` before referencing them in your plugin.
- Attach audio players and speakers to relevant game objects for spatial audio effects.
- Utilize the `TemporaryData` storage for per-player audio setups.

---
