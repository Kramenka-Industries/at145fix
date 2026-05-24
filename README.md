# QoL AT-145 Accuracy Fix

Reverts QoL changes of the AT-145 to sane defaults and keeps key missile behavior customizable.

Original mod by [Neutral Observer](https://github.com/SonPamungkas) and ImpulseNOR.

## Building

1. Restore dependencies:
   ```
   dotnet restore
   ```
2. Build:
   ```
   dotnet build --configuration Release --no-restore
   ```

## Installation

1. Build the mod.
2. Copy `bin/Release/qol145fix.dll` to `BepInEx/plugins/`.
3. Start the game once to generate `BepInEx/config/com.raksaputra.qol145fix.cfg`.

## Configuration

After first run, edit `BepInEx/config/com.raksaputra.qol145fix.cfg` (or use an in-game config editor).

| Section | Setting | Default | Description |
|---------|---------|---------|-------------|
| General | AT-145 Loft Amount | 0.5 | Restores top-attack capability to the AT-145. `0.5` is vanilla/standard. |
| General | Override Motor Parameters | true | If enabled, applies vanilla-like motor values to restore flight behavior and improve accuracy. |
| General | Motor Burn Time | 6.5 | Engine burn time in seconds. Vanilla is `6.5`; QoL mod changed this to `1.0`. |
| General | Motor Thrust | 950 | Engine thrust in Newtons. Vanilla is about `950`; QoL mod changed this to `5000`. |
| General | Motor Fuel Mass | 3.5 | Fuel mass in kg. Vanilla is about `3.5`; QoL mod changed this to `1.0`. |
| Debug | Verbose Logging | false | Logs AT-145 launch before/after values for loft and motor parameters. |

## Behavior Notes

- The patch only applies to `Missile_G2G` (AT-145).
- Loft amount is always updated from config.
- Motor values are only changed when `Override Motor Parameters` is enabled.