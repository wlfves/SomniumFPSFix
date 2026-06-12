# Somnium FPS Fix (Deep-Fried Bug)

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **AI: The Somnium Files**
that fixes a crash and slow-motion glitch that happens during Psyncs when the
game runs at high frame rates.

> Unofficial fan mod. Not affiliated with or endorsed by Spike Chunsoft.

---

## The problem

Inside a Somnium, time slows to a crawl while you stand still and runs at normal
speed while you move. This works fine at 60 FPS.

Above roughly **100 FPS**, standing still instead makes the slow-motion effect
speed up uncontrollably until the game's internal timer overflows and the game
(sometimes) crashes. The higher your frame rate, the faster this happens.

### If your game looks like this when you stand still you have the bug


<img width="2560" height="1440" alt="20260612223434_1" src="https://github.com/user-attachments/assets/a591c224-887a-4fda-9e74-7132bcb0849c" />
<img width="2560" height="1440" alt="20260612223521_1" src="https://github.com/user-attachments/assets/5e80802f-08ee-4e36-8afe-f125766d939c" />

If you've uncapped your frame rate (for example with a high-refresh-rate
monitor) and your Psyncs keep crashing when you stop moving, this is almost
certainly the bug you're hitting.

## A quick history

I first noticed this issue on release day and reported it to Spike the next day

> **From:** wlfves
  > **Date:** Wed, 18 Sep 2019, 20:54
  > **To:** Spike Chunsoft User Support &lt;support-ai@spike-chunsoft.co.jp&gt;
  >
  > Hi Spike Chunsoft,
  >
  > I'm one of the people affected by the problem that seems to happen for users
  > of refresh rates higher than 60fps discussed at
  > https://steamcommunity.com/app/948740/discussions/1/1628538005504421289 .
  >
  > After toggling Anisotropic filtering on and off, my game no longer has the
  > issue, but is locked at 60fps which isn't really ideal.
  >
  > My DxDiag is attached.
  >
  > Thanks for all your help!
  >
  > -wlfves

> **From:** Spike Chunsoft User Support &lt;support-ai@spike-chunsoft.co.jp&gt;
  > **Date:** 24 Sept 2019, 03:18
  > **To:** wlfves
  >
  > Hello,
  >
  > This is Spike Chunsoft User Support.
  >
  > Thank you for your reply.
  >
  > The provided information has been sent to the department in charge and the
  > reported issue is currently under investigation.
  > We are sincerely sorry for the delay.
  > We will inform you about any updates regarding the situation.
  > Thank you for your time and patience.

Cool! Err...

> **From:** wlfves
  > **Date:** 1 Nov 2019, 18:19
  > **To:** Spike Chunsoft User Support &lt;support-ai@spike-chunsoft.co.jp&gt;
  >
  > Hi Spike Chunsoft,
  >
  > I just want to confirm this is still an issue as originally described. All
  > affected users are currently limiting the game to 97FPS during Somniums, then
  > unlimiting the game for smoothness the rest of the game.
  >
  > wlfves

  > **From:** Spike Chunsoft User Support &lt;support-ai@spike-chunsoft.co.jp&gt;
  > **Date:** 5 Nov 2019, 09:39
  > **To:** wlfves
  >
  > Hello,
  >
  > This is Spike Chunsoft User Support.
  > We apologize that it has taken so long to get back to you.
  >
  > The development team is currently investigating the issue.
  > Once the issue is identified, we will soon update the game.
  >
  > We are deeply sorry for the inconvenience.
  > Thank you very much for your great patience and understanding.
  > Kind regards

And it never got fixed, so let's do it ourselves! (Please don't annoy Chunsoft over this, I'm sure this was very very very low down the list of priorities to patch and just got missed somehow).

I dabbled with fixing this a couple of times over the years, but never solved it fully until now. The encryption of the game scripts was the most annoying thing it took me a while to figure out. Then by the time I eventually got around to picking this back up again AI was a thing, so Claude helped me polish the fix.

## What this mod does

It corrects the Somnium script so the slow-motion effect behaves the same way it
does at 60 FPS, at any frame rate, and can never run away.

- The fix is behaviour-preserving: standing still still gives you the gentle
  slow crawl (and the rain audio) exactly like the original game. It is not an
  FPS cap and it does not change how the Somnium feels.
- It is applied to the game's script in memory, as the game loads it. No
  game files are modified on disk, and no game files are bundled with this mod.
- It's fail-safe (hopefully): if a future game update (not that I'd expect one at this point) changes the script so the fix no
  longer fits, the mod refuses to apply a stale patch and logs a warning instead
  (see [Compatibility & safety](#compatibility--safety)).

### Yay! No more deep-fried game!

<img width="2560" height="1440" alt="20260612223436_1" src="https://github.com/user-attachments/assets/67129989-8f69-4763-b5da-285f8ca31653" />
<img width="2560" height="1440" alt="20260612223526_1" src="https://github.com/user-attachments/assets/079a3cd4-7f85-4965-9dda-86391b881025" />


## Requirements

- AI: The Somnium Files (Steam). The fix targets the current Steam build.
- [BepInEx 5.x, 64-bit (x64)](https://github.com/BepInEx/BepInEx/releases).

## Installation

**Download:** grab `SomniumFPSFix.dll` from the
[latest release](https://github.com/wlfves/SomniumFPSFix/releases/latest).

1. **Install BepInEx** if you haven't already:
   - Download the latest **BepInEx 5.x x64** release.
   - Extract it into your game folder — the folder that contains
     `AI_TheSomniumFiles.exe`. You should end up with a `BepInEx` folder next to
     the game's `.exe`.
   - Launch the game once and quit. This lets BepInEx generate its folders.

2. **Install this mod:**
   - Copy `SomniumFPSFix.dll` into `BepInEx/plugins/`.
   - (BepInEx scans that folder recursively, so a subfolder such as
     `BepInEx/plugins/SomniumFPSFix/` works too if you like to keep mods tidy.)

3. **Verify it's working:**
   - Launch the game. On the title screen you should briefly see
     **"Somnium FPS Fix v1.2.0 active"** in the top-right corner. This will only appear at your first visit to the title screen, and disappear when you press "Load" or "New game".
   - In `BepInEx/LogOutput.log` you should see a line like:
     `Applied FPS fix to 'somnium' (... bytes).`

That's it - start a Psync, stand still and hopefully the game won't get deep-fried anymore!

## Configuration

A config file is created at `BepInEx/config/somniumfpsfix.mods.wlf.moe.cfg`
the first time you run the game with the mod installed. You normally don't need
to touch it.

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Turn the fix on or off. |
| `ModuleName` | `somnium` | Name of the script module to patch. The Somnium engine is `somnium`; you should not need to change this. |
| `ExpectedOriginalLength` | `220953` | Size in bytes of the original Somnium script this fix was built for. The fix is only applied when the script matches this size, so a game update can't have an out-of-date patch forced onto it. Set to `0` to skip the check (not recommended). |
| `FallbackInMemoryPatch` | `true` | If the main fix can't be applied, use a simpler safety-net fix instead (see below). |

## How it works

The Somnium script tracks a single "movement amount" number that eases between
**0** (standing still) and **1** (moving), and feeds it into the Somnium's
slow-motion. Every frame, the original script also runs a line that feeds that
number back into itself, slightly larger - it grows by about 1% per frame.

At 60 FPS the script's normal per-frame easing cancels that growth out. But the
higher the frame rate, the less time each frame covers, and above ~100 FPS the
easing can no longer keep up: the number compounds every frame and runs away to
infinity, which overflows and sometimes crashes the game.

Here is the relevant part of the original script (decompiled and lightly tidied
for readability), with the bug annotated:

```lua
-- characterAnimationThirdPersonIK.update -- runs once per frame in a Somnium.
-- 'value' is the movement amount: 0 = standing still, 1 = moving.
-- SOMNIUM_STOP_EFFECT_DURATION = 0.5
-- timer_min_speed = SOMNIUM_MIN_SPEED = 0.01

if eventMode == false then
    -- Ease 'value' toward 1 while moving, or toward 0 while standing still.
    -- This part is correct: it is scaled by Time.deltaTime, so it behaves the
    -- same at any frame rate.
    if currentSpeed > 0.2 then
        value = math.min(value + Time.deltaTime / SOMNIUM_STOP_EFFECT_DURATION, 1)
    else
        value = math.max(value - Time.deltaTime / SOMNIUM_STOP_EFFECT_DURATION, 0)
    end

    -- (a separate 'valueT' is computed here; it drives the countdown timer and
    --  is not part of the bug)

    -- THE BUG: this feeds 'value' back into itself, slightly larger, every
    -- frame. The gain is 1 / (1 - 0.01) = 1.0101, i.e. about +1% per frame.
    -- Crucially it is NOT scaled by Time.deltaTime, so the more frames per
    -- second, the more times per second 'value' gets multiplied up. The easing
    -- above can only pull it back down by a small per-frame amount, so above
    -- ~100 FPS it loses the race and 'value' compounds toward infinity.
    -- (This result was probably meant to be stored in a separate
    --  variable, not written back into 'value'.)
    value = (value + timer_min_speed) / (1 - timer_min_speed)
else
    valueT = 0
    value = 0
end

-- 'value' then drives the slow-motion -- the world time scale, the on-screen
-- stop effect, and the rain audio volume:
--   (s.rootNode):UpdateTimeScale(value)
```

The fix does two things:

1. **Removes** the line that feeds the number back into itself.
2. **Sets the standing-still and moving values directly** to the numbers the
   original game would have settled on anyway (`0.0101` standing still, `1.0202`
   moving).

The result is identical to the original at normal frame rates and can never run
away.

## Compatibility & safety

- **No game files are shipped or modified on disk.** The fix is applied to the
  script in memory while the game loads it.
- **Version-guarded.** The fix only applies when the original Somnium script is
  exactly the size it expects (`ExpectedOriginalLength`). If a game update
  changes it, the main fix is skipped so you can never get a mismatched patch.
- **Safety-net fallback.** If the main fix can't be applied, the mod can fall
  back to simply blanking out the runaway line. That still prevents the crash,
  but standing still becomes a full freeze (and the rain sound goes silent)
  instead of the slow crawl. This is a backstop only; if you see the warning
  about the fallback in your log, the mod likely needs updating for a new game
  version.

## Troubleshooting

- **No "active" label on the title screen / no log line:** BepInEx isn't loading
  the mod. Check that `SomniumFPSFix.dll` is somewhere under
  `BepInEx/plugins/`, that you installed the **x64** build of
  BepInEx, and that `BepInEx/LogOutput.log` shows BepInEx starting up.
- **Log warns about an unexpected script size:** the game has probably been
  updated and this mod needs a new release. Until then, the fallback (if
  enabled) will keep the game from crashing.

## Building from source

This project targets .NET Framework 3.5 (to match the game's runtime) and
references several assemblies from the game install, which are not included
in this repository because they're copyrighted game files.

1. Clone the repository.
2. Create a `Lib/` folder next to the `.csproj` and copy these four DLLs into it
   from your game's `AI_TheSomniumFiles_Data/Managed/` folder:
   - `Assembly-CSharp.dll`
   - `Assembly-CSharp-firstpass.dll`
   - `TextMeshPro-2017.3-Runtime.dll`
   - `UnityEngine.UI.dll`
3. Build:
   ```
   dotnet build -c Release (or hit build in Visual Studio)
   ```
   The plugin DLL is produced under `bin/Release/`.

> A `Debug` build also copies the DLL into a hard-coded Steam install path as a
> convenience (see the `CopyToGamePlugins` target in the `.csproj`); edit or
> remove that if your game is installed elsewhere. The `Release` build does not.

## Extracting the game's Lua scripts

The game's logic is written in Lua. If you want to read the scripts yourself —
to verify this fix, regenerate it after a game update, or build a different mod, you need to be able to work with them.

Every Lua module is compiled to Lua 5.1 bytecode and stored
inside a Unity AssetBundle as a `TextAsset`, at the path
`Assets/AutoGen/LuaByteCode/<module>.bytes` (for example, `somnium.bytes`).

### Encryption

This is the important part: pulling the `.bytes` out with
a tool like AssetStudio or AssetRipper gives you something that looks a lot like sLUA(?) that won't
decompile - Very annoying. The game decrypts each module in memory
right before running it, using a simple counter-based XOR cipher:

- For every byte at index `i >= 4`, XOR it with `i & 0xFF` (the low 8 bits of its
  own position).
- The first 4 bytes are left untouched — they're the plaintext Lua signature
  `1B 4C 75 61` (`"\x1bLua"`).
- XOR is symmetric, so the exact same routine both decrypts and re-encrypts.

The reference implementation, taken from the game's own loader
(`ScriptManager.LoaderDelegate`), is:

```csharp
// bytes = the raw TextAsset contents from the AssetBundle
for (int i = 4; i < bytes.Length; i++)
    bytes[i] ^= (byte)(i & 255);
// bytes is now standard Lua 5.1 bytecode
```

After decryption the data is plain Lua 5.1 bytecode (header `1B 4C 75 61 51 ...`)
and decompiles with standard tools such as
[luadec](https://github.com/viruscamp/luadec) or
[unluac](https://github.com/HansWessels/unluac).

**Included tool.** [`tools/decrypt_assets.py`](tools/decrypt_assets.py) applies
the cipher above. It verifies the Lua signature and version byte after decrypting
so you know the result is valid. Requires only Python 3.

```bash
# Decrypt a single module:
python3 tools/decrypt_assets.py somnium.bytes somnium.luac

# Or decrypt a whole folder of extracted .bytes files at once:
python3 tools/decrypt_assets.py --all <extracted_dir> <output_dir>
```

## Credits

- Fix and mod by wlfves, working together with Claude
- Built on [BepInEx](https://github.com/BepInEx/BepInEx) and
  [HarmonyX](https://github.com/BepInEx/HarmonyX).

## License

[GNU General Public License v3.0](LICENSE) (GPLv3). If you distribute this mod or
a modified version, you must make the corresponding source available under the
same license.
