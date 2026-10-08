# DosBoxControl — Claude Code skill

A Claude Code skill (`dosbox-control`) that lets Claude launch DOS programs in DOSBox,
take screenshots of the window, send keystrokes, and close it. It works through a small
local HTTP API (ASP.NET Core, .NET 8, Win32 `PrintWindow`/`SendInput`).

The compiled binaries are **not** committed (`bin/` and `obj/` are git-ignored), so you
build them yourself and drop them into the skill folder.

## Repository layout

```
skill/
  SKILL.md            # the skill definition Claude Code loads
  bin/                # (not in git) published exe goes here
src/
  DosBoxControl/      # the .NET web API source
  DosBoxControl.slnx
  README.md           # detailed API reference
```

## Prerequisites

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [DOSBox 0.74-3](https://www.dosbox.com/) (default path `C:\Program Files (x86)\DOSBox-0.74-3`)

## Build the binaries

From the repo root:

```powershell
dotnet publish src/DosBoxControl/DosBoxControl.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o skill/bin
```

This produces `skill/bin/DosBoxControl.exe` plus `appsettings.json`. If you'd rather
rely on an installed ASP.NET Core 8 runtime, use `--self-contained false` for a smaller
output.

If DOSBox is installed somewhere else, edit `DosBox:InstallDir` in
`skill/bin/appsettings.json`.

## Install the skill into Claude Code

Copy (or symlink) the `skill` folder into a Claude Code skills directory, naming the
folder `dosbox-control`:

**Personal (available in all projects)**

```powershell
Copy-Item -Recurse skill "$env:USERPROFILE\.claude\skills\dosbox-control"
```

**Single project**

```powershell
Copy-Item -Recurse skill "<your-project>\.claude\skills\dosbox-control"
```

Restart Claude Code (or start a new session). The skill should appear as
`dosbox-control`. Verify by asking Claude: *"Open C:\Games\SOMEGAME\GAME.EXE in DOSBox
and take a screenshot."*

Claude starts the server itself on first use (`skill/bin/DosBoxControl.exe`, listening
on `http://localhost:5164`). To test manually:

```powershell
.\skill\bin\DosBoxControl.exe
curl http://localhost:5164/api/status
```

> **Note:** `SKILL.md` mentions the author's source path
> (`F:\priyan\Projects\AI\tools\claude\dosboxcontrol`). Update that line to wherever
> you cloned the repo.

## Rebuilding after changes

Close any running `DosBoxControl.exe` (it locks the file), re-run the publish command,
and copy `skill/bin` over the installed skill's `bin` folder.

## More information

See [`src/README.md`](src/README.md) for the full API reference (endpoints, key syntax,
notes on DOSBox windows).
