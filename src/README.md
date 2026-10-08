# DosBoxControl

A small local HTTP API (Windows only) for controlling a DOSBox window from another
project: launch a DOS program by path, take screenshots of the DOSBox window, send
keystrokes to it, and close it.

Built with ASP.NET Core minimal APIs (.NET 8) using Win32 interop (`PrintWindow`,
`SendInput`) — no external automation libraries.

## Requirements

- Windows
- .NET 8 SDK
- DOSBox installed (default configured path: `C:\Program Files (x86)\DOSBox-0.74-3`,
  change via `DosBox:InstallDir` in `src/DosBoxControl/appsettings.json`)

## Run

```
dotnet run --project src/DosBoxControl
```

By default it listens on `http://localhost:5164` (see `Properties/launchSettings.json`).

## API

### `POST /api/open`
Launches DOSBox, mounts the target file's directory as `C:`, and runs it
(uses DOSBox's built-in "run this file" argument — no config file needed).

```json
{ "exePath": "C:\\Games\\SOMEGAME\\GAME.EXE", "args": null, "fullscreen": false }
```
Only one session is tracked at a time; opening a new one closes the previous session
first. Returns `{ "pid": ..., "windowHandle": ..., "windowTitle": ... }`.

### `GET /api/screenshot`
Returns a PNG (`image/png`) of the current DOSBox window, captured via `PrintWindow`.
404 if no session is open.

### `POST /api/keys`
Sends keystrokes to the DOSBox window via `SendInput` (brings the window to the
foreground first). DOSBox reads raw keyboard input, so this is more reliable than
posting window messages.

```json
{ "text": "optional literal text to type", "keys": ["ENTER", "UP", "ALT+ENTER"], "delayMs": 30 }
```
`text` types out literal characters (handles shift for punctuation/uppercase).
`keys` sends named keys/chords: arrows (`UP`/`DOWN`/`LEFT`/`RIGHT`), `ENTER`, `ESC`,
`TAB`, `SPACE`, `BACKSPACE`, `F1`-`F24`, `CTRL`/`ALT`/`SHIFT` (combine with `+`, e.g.
`ALT+ENTER` for DOSBox's fullscreen toggle), single letters/digits.

### `POST /api/close`
Closes the current session. `{ "force": false }` (default) posts `WM_CLOSE` and
waits up to 3s before force-killing; `{ "force": true }` kills immediately.

### `GET /api/status`
Returns whether a session is active, its pid, exe path, and window info.

## Notes

- Only a single DOSBox session is tracked. Opening a second one closes the first.
- DOSBox 0.74-3 opens two top-level windows on some builds: the actual render
  window (class `SDL_app`) and a `DOSBox Status Window` debug console (class
  `ConsoleWindowClass`). The API always targets the render window.
