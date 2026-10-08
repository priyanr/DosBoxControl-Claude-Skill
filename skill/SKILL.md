---
name: dosbox-control
description: Control a DOSBox window on this Windows machine — launch a DOS .exe/.com/.bat by path, take a screenshot of the DOSBox window, send keystrokes/text to it, check status, and close it. Use whenever asked to run/open a DOS program, screenshot DOSBox, send keys/input to DOSBox, or automate a DOS game/app.
---

# DOSBox Control

A local HTTP API wraps DOSBox automation (Win32 `PrintWindow` for screenshots,
`SendInput` for keystrokes — DOSBox reads raw input, not window messages, so
`SendInput` is required for keys to register).

Source/project: `F:\priyan\Projects\AI\tools\claude\dosboxcontrol` (edit and rebuild
there if behavior needs to change; this skill runs the published standalone exe at
`bin\DosBoxControl.exe` next to this file).

Server listens on `http://localhost:5164`. Only one DOSBox session is tracked at a
time — opening a new one closes whatever was open.

## Start the server (if not already running)

Check first:
```
curl -s http://localhost:5164/api/status
```
If that fails to connect, start it in the background from this skill's directory
(do not use `dotnet run` — there's no source here, just the published exe):
```
cd <this skill's directory>\bin
./DosBoxControl.exe
```
Run it as a background/detached process (e.g. PowerShell `Start-Process` or the
equivalent backgrounding your tool supports) — it's a long-running server, not a
one-shot command. Wait ~1-2s then re-check `/api/status` to confirm it's up.

DOSBox install dir defaults to `C:\Program Files (x86)\DOSBox-0.74-3`
(`bin\appsettings.json`, key `DosBox:InstallDir`) — update that file if DOSBox is
installed elsewhere.

## Open a DOS program

```
POST /api/open
{ "exePath": "C:\\Games\\SOMEGAME\\GAME.EXE", "args": null, "fullscreen": false }
```

**Path must use backslashes (JSON-escaped as `\\`), not forward slashes.** DOSBox's
own "mount the basedir and run this file" argument parsing looks for `\` to find the
containing directory — a forward-slash path still passes .NET's `File.Exists` check
and DOSBox will start, but it lands at the plain `Z:\>` prompt instead of mounting
and running the target (this was verified empirically, not a guess).

Response: `{ "pid": ..., "windowHandle": ..., "windowTitle": ... }`. It can take a
couple seconds for the DOSBox window to appear — the endpoint waits for it (up to 8s)
before responding, so a slow response is normal, not a hang.

## Screenshot

```
GET /api/screenshot
```
Returns a PNG. 404 if no session is open (call `/api/open` first).

## Send keystrokes

```
POST /api/keys
{ "text": "dir /w", "keys": ["ENTER"], "delayMs": 30 }
```
- `text`: literal characters to type (handles shift for punctuation/uppercase).
- `keys`: named keys or chords joined with `+` — `ENTER`, `ESC`, `TAB`, `SPACE`,
  `BACKSPACE`, `UP`/`DOWN`/`LEFT`/`RIGHT`, `F1`-`F24`, `CTRL`/`ALT`/`SHIFT` combos
  (e.g. `ALT+ENTER` toggles DOSBox fullscreen), single letters/digits.
- Both fields can be sent together; `text` is sent first, then `keys`.
- This brings the DOSBox window to the foreground before sending — expect focus to
  shift away from whatever window currently has it.

## Check status

```
GET /api/status
```
`{ "running": bool, "pid": ..., "exePath": ..., "windowTitle": ..., "windowHandle": ... }`

## Close

```
POST /api/close
{ "force": false }
```
`force: false` (default) posts a graceful close and waits up to 3s before
force-killing; `force: true` kills immediately.

## Gotchas learned from testing

- DOSBox 0.74-3 can open a second window, `DOSBox Status Window` (a debug console) —
  the API already filters this out and always targets the real render window, no
  action needed on the caller's side.
- The exe/bat file must actually exist on disk (`FileNotFoundException` → 500 if not).
- If DOSBox.exe isn't found at the configured `DosBox:InstallDir`, `/api/open` 500s —
  check `bin\appsettings.json` if that happens.
