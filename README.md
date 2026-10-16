# Winverter2

Winverter2 is a Windows Forms application that inverts the colours of other applications' windows. Use it to make light windows darker without changing the applications themselves.

## Features

- Invert several applications at once, including their dialogs and other top-level windows.
- Save applications in **Always Invert** and automatically apply inversion when they run.
- Filter the running application list to processes owned by the current Windows user.
- Continue clicking and typing through the inversion overlays.
- Follow windows as they move, resize, minimize, or change monitors.
- Support monitors with different DPI scaling settings.
- Use a dark interface with selections and scroll position preserved during refresh.

Winverter excludes itself from the application lists and inversion.

## Requirements

- 64-bit Windows with a graphical desktop.
- [.NET 8 Desktop Runtime for Windows (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) to run a framework-dependent build.
- .NET 8 SDK or a later SDK supporting .NET 8 to build from source. Visual Studio with the **.NET desktop development** workload is optional.

## Build and run

Clone or download this repository, then open a terminal in the solution folder:

```powershell
dotnet build Winverter2/Winverter2.csproj
dotnet run --project Winverter2/Winverter2.csproj
```

Alternatively, open `Winverter2.slnx` in a compatible Visual Studio version, or open `Winverter2/Winverter2.csproj` directly, and press **F5**.

Exit any running copy using the notification icon's **Exit Winverter** menu before rebuilding to avoid locking the executable.

## Invert running applications

1. Start Winverter. The upper list shows processes with open windows, including minimized windows.
2. Select an application to invert its visible windows.
3. Hold **Ctrl** and click to add or remove individual applications. Hold **Shift** and click to select a range.
4. Deselect an application to restore its colours.

The list refreshes automatically every two seconds. Use **Refresh** to scan immediately. New windows belonging to selected processes are picked up automatically, and minimized windows are inverted when restored.

**Only processes owned by the current user** is checked by default. Uncheck it to include other users' processes where Windows permits access. Processes whose owner cannot be read are omitted while the filter is enabled. The filter also applies to Always Invert matches.

## Always Invert

To remember an application between runs:

1. Select one or more applications in the upper list.
2. Click **Add selected to Always Invert**.

You can also click **Add executable...** to choose one or more `.exe` files, including applications that are not currently running.

The lower list shows each saved executable's name and full path. Matching uses the full path and ignores letter case. Executables with the same name in different folders are treated separately. All matching running processes with open windows are inverted and excluded from the upper list.

Saved rules are checked when Winverter starts and during each two-second refresh. If an executable moves or its installation path changes, remove the old entry and add the new one.

To stop remembering an application, select its entry in **Always Invert** and click **Remove selected**. Its running windows return to their original colours, and the process becomes available in the upper list.

### Saved settings

Changes are saved immediately to:

```text
%LOCALAPPDATA%\Winverter2\always-invert.json
```

Rules persist between application runs and Windows restarts. Launch Winverter again after restarting Windows to apply them. Winverter does not automatically register itself to launch at sign-in.

## Pause or stop inversion

- Click **Stop inversion** to restore all windows and pause automatic inversion.
- Click **Resume inversion** to resume saved Always Invert rules. Manually selected applications need to be selected again.
- Closing the main window hides it in the notification area; inversion and automatic detection keep running.
- Double-click the crescent-moon notification icon, or right-click it and choose **Open Winverter**, to reopen the window.
- To shut down completely and remove all inversion overlays, right-click the notification icon and choose **Exit Winverter**.

The notification icon is present while Winverter is running, whether the main window is visible or hidden. Windows may place it in the notification area's overflow menu; use Windows taskbar settings to pin it if desired.

### Start without opening the main window

Enable **Start in notification area** to start future runs with only the notification icon visible. This defaults to unchecked on first use and is saved immediately in `%LOCALAPPDATA%\Winverter2\preferences.json`. Always Invert rules and periodic scanning still run when the window is hidden. Reopen the window through the notification icon to change the setting. Uncheck it to show the main window on future launches. This setting does not register the application to launch at Windows sign-in.

Pausing does not delete saved rules. The paused state is not retained between runs.

## Limitations and troubleshooting

- Protected applications, secure desktops, capture-protected video, exclusive fullscreen content, and some GPU-rendered surfaces may not invert.
- Windows may prevent Winverter from reading a process's executable path or accessing a protected process. Such processes cannot be matched automatically.
- Windows belonging to separate helper processes require their own selections or saved rules.
- Occlusion is approximated with rectangular bounds; transparent or unusually shaped windows may show imperfect clipping.
- If monitor scaling changes, restart Winverter if alignment appears incorrect. The application uses per-monitor DPI awareness and physical-pixel overlay coordinates.

Inversion uses the [Windows Magnification API](https://learn.microsoft.com/en-us/windows/win32/winauto/magapi/magapi-intro) at 1:1 scale. The project targets x64 because this API does not support a 32-bit application running under WOW64 on 64-bit Windows. No target process is modified.

## Development checks

The executable, main window, and notification area share a transparent crescent-moon icon with sizes from 16 to 256 pixels. To regenerate the assets on Windows, run `./scripts/New-AppIcon.ps1` in PowerShell.

Run the persistence checks from the repository root:

```powershell
dotnet run --project tests/StateTests
dotnet run --project tests/StartupTests
```

For manual verification, test multiple selections, dialogs, overlapping windows, minimize/restore, and movement between monitors with different scaling. Add and remove Always Invert rules, restart Winverter, and close/relaunch a matching application to verify persistence and automatic detection. Check that Stop/Resume and the current-user filter behave as expected.

## Licence

Winverter2 is licensed under the **GNU General Public License, version 2 only** (`GPL-2.0-only`). See [LICENSE](LICENSE) for the complete terms.
