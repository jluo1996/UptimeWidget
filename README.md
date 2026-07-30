# UptimeWidget

A modern, lightweight Windows desktop widget that shows system information (such as uptime) in a small, customizable overlay that floats on your desktop and lives in the system tray.

## Download

Grab the latest installer from the **[Releases](https://github.com/jluo1996/UptimeWidget/releases)**.

## Requirements

UptimeWidget runs on Windows versions that support the **.NET 10 Runtime**. If the runtime isn't already present, the installer automatically installs the latest available version of .NET 10, so you don't need to download it separately.

To view the latest list of all supported Windows versions, please refer to Microsoft's [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions) documentation.

## Getting Started

1. Download and run the installer from the **[Releases](https://github.com/jluo1996/UptimeWidget/releases)** page.
2. Launch **UptimeWidget**. The widget appears on your desktop and an icon is added to the system tray (bottom-right of the taskbar).
3. Use the tray icon to open settings, show/hide the widget, or exit the app.

## Features

### The Widget

- **Move it:** Drag the widget with your mouse to place it anywhere on the screen. Its position is remembered next time you start the app.
- **Always on top:** Keeps the widget visible above other windows (can be turned off in Settings).
- **Lock position:** Locks the widget in place so it can't be moved by accident, and lets mouse clicks pass through to whatever is behind it.

### Tray Menu

Right-click the tray icon to access:

- **Settings…** — Open the settings window to customize the widget.
- **Show widget** — Show or hide the widget.
- **Lock position** — Lock or unlock the widget's position.
- **About…** — View version information.
- **Exit** — Close the app.

Double-click the tray icon to quickly show or hide the widget.

### Settings

Open **Settings…** from the tray menu to customize the widget. Changes preview live and are saved when you click **OK** (click **Cancel** to discard them):

- **Metrics:** Choose which pieces of information appear on the widget (for example, system uptime).
- **Opacity:** Adjust how transparent the whole widget is.
- **Background opacity:** Adjust the background transparency independently of the text.
- **Update interval:** Set how often the displayed values refresh.
- **Font size:** Make the text larger or smaller.
- **Text color / Background color:** Pick your preferred colors.
- **Always on top:** Keep the widget above other windows.
- **Start with Windows:** Launch UptimeWidget automatically when you sign in.

## Interaction Reference

| Action | Result |
| --- | --- |
| Drag the widget | Move it (position is remembered) |
| Double-click tray icon | Show / hide the widget |
| Right-click tray icon | Open the tray menu |
| Lock position (tray/Settings) | Prevent moving and let clicks pass through |
