# Technical notes

Target: French release of `Devil.exe` (3,215,360 bytes, as shipped by Abandonware France). File offsets below
are for that build; `.text` starts at VA `0x401000` / file offset `0x1000`. Every patch checks the original bytes
first and refuses to write on any other build.

## Patches

| Id | File offset | VA | Original → patched | Why |
|---|---|---|---|---|
| `horloge` | `0x1BB2A5` | `0x5BB2A5` | `74 68` → `EB 68` | The engine clock (`0x5BB290`) uses RDTSC with a frequency of `MHz × 1,000,000` computed in 32 bits: overflow above 4294 MHz (a 4.7 GHz CPU gives ~405 MHz, game ~11.6× too fast). The jump forces the existing `QueryPerformanceCounter` path. |
| `cd` | `0xA2DB9` | `0x4A2DB9` | `75 36` → `EB 36` | Skips the "insert the CD" `MessageBox` loop (MCI `STATUS_MEDIA_PRESENT` check). |
| `musique` | `0x2E5DEA` | – | `WINMM.dll` → `WinMX.dll` | Import name only: the bundled `WinMX.dll` winmm proxy plays `Music\TrackNN.ogg` as CD audio. |
| `fov` | `0x1B83D9` + cave `0x84F0`, value `0x8500` | `0x5B83D9`, `0x4084F0`, `0x408500` | `fild [ecx+0xC4]` → `call 0x4084F0; nop` | Camera projection (`0x5B83B0`): focal = `640 / (2·tan(fov/2))` for a 640x480 reference, then scaled by width, so 16:9 crops top and bottom. The cave multiplies the focal by `K = (4/3) / (W/H)` (Hor+), written at launch. Verified live: camera `0x6EE540` focal 409.6 → 307.2 at 2560x1440. |
| `surfaces` | `0x19186C` + cave `0x8510` | `0x59186C`, `0x408510` | `call [eax+0x64]; test eax,eax` → `call 0x408510` | `IDirectDrawSurface::Lock` after Alt+Tab returns `DDERR_SURFACELOST` and the engine throws. Flip (`0x59503A`) already restores with `RestoreAllSurfaces` (`0x595130`); the cave does the same for Lock (restore, `Sleep(50)`, retry up to 20×). |
| `entrees` | `0x111CD3`, `0x17E881`, `0x193C40`, `0x193D34` | `0x511CD3`, `0x57E881`, `0x593C40`, `0x593D34` | see source | DirectInput `Acquire` fails without focus (`E_ACCESSDENIED`, "cannot acquire keyboard/mouse") and is fatal. Per-frame reads leave the loop with the error (already handled as "no input"); the two unchecked reads wait 50 ms and retry. |

The caves live in `int3` padding at `0x4084F0–0x4085D0`; the executable has no ASLR (`DllCharacteristics = 0`).

## Other findings

- **Setup crash** `0x0040497D`: `Devil Setup.exe` keeps a linked list of 16-bit display modes; the 2016 DDrawCompat
  enumerated none, and selecting a mode walked a null list. With dgVoodoo (or native DirectDraw) the setup works,
  but Windows elevates it automatically because its name contains "Setup". The launcher replaces it.
- **Second instance crash** `0x0067B2FA` (`CString` copy from address 4): reproduced by starting two `Devil.exe`;
  a single instance under a debugger raised no access violation.
- **Resolution**: `Config.ini` `Resolution = WxH` is used as is; the engine renders natively at 2560x1440. dgVoodoo
  adds the chosen resolution to its enumerated list (`ExtraEnumeratedResolutions`).
- **Floating cameras** (`FloatingCameras`, bit `0x100` of `0x6FAA48`) is a camera behaviour option, not a shake.

## Launcher

- C# 5 / .NET Framework 4, WinForms, built with the Windows `csc` (`compiler.bat`).
- Layered window (`UpdateLayeredWindow`) with per-pixel alpha: the TV outline comes from the original mask
  bitmap (magenta = outside) scaled with bicubic filtering, so edges are smooth.
- Art loaded from `Devil Setup.exe` resources (`BITMAP` 136 TV, 146 mask, 180/183/184/187 knob strips,
  188 centre knob glow, 162–174 H/M/L, 145/138 Launch/Quit…). Square sprite backgrounds are cut to soft discs.
- CRT screen: content cached and redrawn only when it changes; only the scan band animates (time based, ~60 fps).
  Screen effects are masked by the glass shape found by flood-filling the bright bezel from the edges.
- `DevilInsidePlus.exe /capture file.png [page]` renders the launcher to a transparent PNG (used for `docs/`).
