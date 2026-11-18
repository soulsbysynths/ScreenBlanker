# ScreenBlanker

A menu-bar app that blacks out every display when a countdown ends, or at a set time each day.
Type the exit keys (**Esc** by default, or any sequence you set) to remove the blackout. Built with Unity 2022.3. macOS is the main target, and it
also runs on Windows so you can develop and test on a PC.

## Using it

- **Status item** (macOS menu bar / Windows tray): shows the countdown next to the icon while it
  runs. Its menu lists the time left, the next daily blackout and the number of displays, and has
  Start/Pause, Stop, Blackout now, Settings… and Quit. The icon is dimmed when nothing is scheduled.
- **Settings window** (version number in the title bar and header): countdown (h/m/s) with
  Start/Pause/Resume and Stop; a daily blackout time with an on/off checkbox; a **Blackout** card;
  *Start at login*; Test blackout; Hide window; Quit.
- **Blackout card**:
  - *Show prompt* and *Show graphic* switch the two parts of the moving overlay on and off
    independently. With both off the screens are plain black.
  - The graphic defaults to the built-in logo. *Choose…* picks any PNG or JPEG; *Use built-in* goes
    back to the logo. It is shown at up to 180 points, with the prompt centred beneath it.
  - *Keys to end the blackout*: click **Listen**, press the keys in order (up to 10, modifiers
    included), then click **Stop listening** or press **[Ctrl] [Enter]**. Nothing typed keeps the old
    keys; *Reset to [Esc]* restores the default. Left and right Shift/Ctrl/Alt/Cmd count as the same
    key. The prompt reads e.g. "Press [Ctrl] [Shift] Q": control keys in square brackets, letters and
    digits plain.
- When launched at login the app starts hidden. Opening the app again shows the settings window.
- The mouse pointer is hidden while the blackout is up, and comes back when it is dismissed.
- Keys are matched in order; a wrong key starts the sequence again. Shortcuts such as Cmd+Q or
  Alt+F4 are swallowed while the blackout is up.
- If the computer was asleep or off at the daily time, that day's blackout is skipped. It is not
  fired late.

## Building (on the PC)

Use the **ScreenBlanker** menu in the Unity editor. **Every build command bumps the patch version**
(1.0.4 becomes 1.0.5, and the macOS build number goes up by one), so each build has its own number.
The version is stored in `ProjectSettings/ProjectSettings.asset`; commit it with the build. To
change the major or minor number, edit Player Settings > Version; the bumps continue from there.

- **Build macOS**: writes `Builds/macOS/ScreenBlanker.app` (universal: Intel + Apple silicon) and
  `finalise-mac-build.sh`.
- **Build Windows**: writes `Builds/Windows/ScreenBlanker.exe`.
- **Build All (macOS + Windows Installer)**: both, under one shared version number.
- **Build Windows Installer**: builds Windows, then packages it as
  `Builds/Installer/ScreenBlanker-Setup-<version>.exe` with Inno Setup 6 (`Installer/ScreenBlanker.iss`).
  It installs per user, with no admin prompt. It adds a Start-menu shortcut and an optional desktop shortcut.
  The uninstaller also removes the start-at-login entry.
- **Apply Project Settings**: re-applies the player settings (runs once automatically).
- **Set Up Scene**: puts the settings window into `Assets/Scenes/SampleScene.unity` (see below).
  Safe to run again.

## Editing the settings window

The window is UI Toolkit, and you can see it in the editor's Game view without pressing Play:

- **Layout**: `Assets/ScreenBlanker/UI/SettingsWindow.uxml`. Open it in **Window > UI Toolkit >
  UI Builder** to edit it visually. Each h/m/s field is an instance of `Stepper.uxml`.
- **Look** (colours, fonts, sizes, spacing): `Assets/ScreenBlanker/UI/SettingsWindow.uss`. The
  colours are variables at the top.
- **Scaling**: the layout is designed at 860×620 and scaled to fit the window
  (`ScreenBlankerPanelSettings.asset`). The built app's window is 1075×775 (1.25×).
- **Behaviour**: `Scripts/SettingsView.cs` only looks elements up **by name** and wires them to the
  app. Renaming or deleting a named element in UI Builder breaks that link. Restyling, moving,
  resizing and adding unnamed elements are all fine. A missing name is reported clearly at start-up.
- The `ScreenBlanker` object in SampleScene carries the UIDocument, `BlankerApp` and `SettingsView`.
  If it goes missing, run **ScreenBlanker > Set Up Scene**.
- UI Builder re-indents files with spaces when it saves; that's expected.

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

The AppKit and Win32 work (status item, blackout windows, key handling, file dialogs) is called directly from
C#: Objective-C runtime calls on macOS and P/Invoke on Windows. There is no native plug-in to
compile, so a Mac is never needed to build. See `Assets/ScreenBlanker/Scripts/Platform`.

The settings window is UI Toolkit (see above). The only IMGUI left is `InAppBlackoutGui`, the
blackout drawn inside the Unity window on platforms with no native host (never Mac or Windows).

StyleCop runs through `Assets/Default.ruleset`. `Assets/csc.rsp` passes `stylecop.json` to the
compiler. The editor compile picks this up, but Unity's player-build compile ignores it, so build
logs still show SA1027 (tabs) warnings. Those are expected.
