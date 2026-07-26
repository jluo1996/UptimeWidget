# UptimeWidget Installer — Install Layout

The UptimeWidget installer is a **per-user** MSI (`Scope="perUser"`), so it
installs without administrator privileges and places all files under the current
user's profile.

## Installed Locations

### 1. Application Folder

```
%LocalAppData%\github@jluo1996\UptimeWidget\
```

Directory structure defined in `Folders.wxs`:

- `LocalAppDataFolder`
  - `ManufacturerFolder` — name = `!(bind.Property.Manufacturer)` → `github@jluo1996`
    - `INSTALLFOLDER` — name = `!(bind.Property.ProductName)` → `UptimeWidget`

Contents placed here (defined in `AppComponents.wxs`):

| File | How it's added |
|------|----------------|
| `UptimeWidget.exe` | Explicit `MainExecutable` component |
| All other payload files (`.dll`, `.deps.json`, `.runtimeconfig.json`, subfolders, etc.) | Auto-harvested via `<Files Include="$(var.PayloadDir)**">`, excluding `UptimeWidget.exe` |

The payload comes from `PayloadDir`, which defaults to:

```
..\..\UptimeWidget\UptimeWidget\bin\x64\<Configuration>\net10.0-windows\
```

(and can be overridden in CI, e.g. `-p:PayloadDir=C:\path\to\artifact\`).

### 2. Start Menu Shortcut

```
%AppData%\Microsoft\Windows\Start Menu\Programs\UptimeWidget\UptimeWidget.lnk
```

- `ProgramMenuFolder`
  - `AppProgramMenuFolder` (named "UptimeWidget")
    - Shortcut "UptimeWidget" → installed `UptimeWidget.exe`

### 3. Desktop Shortcut

```
%UserProfile%\Desktop\UptimeWidget.lnk
```

Placed directly in `DesktopFolder`, pointing to the installed `UptimeWidget.exe`.

## Uninstall Behavior

- `RemoveFolder` cleans up the per-user Start Menu subfolder (`AppProgramMenuFolder`).
- Standard MSI removal deletes installed files and shortcuts.

