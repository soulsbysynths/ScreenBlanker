# ScreenBlanker

A menu-bar app that blacks out every display when a countdown ends, or at a set time each day.
Press **Esc** to remove the blackout. Built with Unity 2022.3. macOS is the main target, and it
also runs on Windows so you can develop and test on a PC.

## Using it

- **Status item** (macOS menu bar / Windows tray): shows the countdown next to the icon while it
  runs. Its menu lists the time left, the next daily blackout and the number of displays, and has
  Start/Pause, Stop, Blackout now, Settings… and Quit. The icon is dimmed when nothing is scheduled.
- **Settings window**: countdown (h/m/s) with Start/Pause/Resume and Stop; a daily blackout time
  with an on/off checkbox; *Show moving "Press Esc" prompt* (turn it off for plain black);
  *Start at login*; Test blackout; Hide window; Quit.
- When launched at login the app starts hidden. Opening the app again shows the settings window.
- The mouse pointer is hidden while the blackout is up, and comes back when Esc is pressed.
- If the computer was asleep or off at the daily time, that day's blackout is skipped. It is not
  fired late.

## Building (on the PC)

Use the **ScreenBlanker** menu in the Unity editor:

- **Build macOS**: writes `Builds/macOS/ScreenBlanker.app` (universal: Intel + Apple silicon) and
  `finalise-mac-build.sh`.
- **Build Windows**: writes `Builds/Windows/ScreenBlanker.exe`.
- **Build Windows Installer**: builds Windows, then packages it as
  `Builds/Installer/ScreenBlanker-Setup-<version>.exe` with Inno Setup 6 (`Installer/ScreenBlanker.iss`).
  It installs per user, with no admin prompt. It adds a Start-menu shortcut and an optional desktop shortcut.
  The uninstaller also removes the start-at-login entry. The version comes from Player Settings > Version.
- **Apply Project Settings**: re-applies the player settings (runs once automatically).

## Installing on the Mac

1. Copy `ScreenBlanker.app` and `finalise-mac-build.sh` to the Mac, keeping them in the same folder.
2. Run `bash finalise-mac-build.sh` once. It restores the executable bit, which is lost on Windows,
   clears quarantine, and ad-hoc signs the app.
3. Optionally move the app to `/Applications`, then open it. The login item is refreshed with the
   app's current location on every launch.

### Making a DMG

Copy the whole `Builds/macOS` folder to the Mac and run `bash make-dmg.sh`. It signs the app
(running `finalise-mac-build.sh` for you) and creates `ScreenBlanker-<version>.dmg` alongside it.
The DMG is compressed and has the usual drag-to-Applications layout. It has to be made on the Mac
because only macOS can sign the app and build a disk image.

The app is ad-hoc signed, not notarised (that needs a paid Apple Developer ID). On another Mac,
opening a downloaded copy for the first time is blocked. To allow it, try to open it once, then go to
System Settings > Privacy & Security and click "Open Anyway". Copies brought over on a USB stick
usually aren't blocked.

Start at login uses `~/Library/LaunchAgents/com.soulsbysynths.screenblanker.plist`. On Windows it
uses the `HKCU\…\Run` key.

## How it works

The AppKit and Win32 work (status item, blackout windows, Escape handling) is called directly from
C#: Objective-C runtime calls on macOS and P/Invoke on Windows. There is no native plug-in to
compile, so a Mac is never needed to build. See `Assets/ScreenBlanker/Scripts/Platform`.

StyleCop runs through `Assets/Default.ruleset`. `Assets/csc.rsp` passes `stylecop.json` to the
compiler. The editor compile picks this up, but Unity's player-build compile ignores it, so build
logs still show SA1027 (tabs) warnings. Those are expected.
