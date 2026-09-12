# Lockscreen Studio

A lightweight Windows lockscreen customization studio built with .NET 8 and WPF.

## MVP features

- Live 1920x1080 lockscreen preview
- Background image picker
- Live clock and date modules
- Custom text module
- Adjustable clock and text sizing
- PNG export
- Apply flow that renders the current design and opens Windows Lock Screen settings

## Run

Requirements: Windows 10/11 and .NET 8 SDK.

```powershell
dotnet run --project .\LockscreenStudio\LockscreenStudio.csproj
```

Or open `LockscreenStudio.sln` in Visual Studio 2022 and run the project.

## Architecture direction

The current MVP intentionally treats the Windows lockscreen as a rendered composition. The next iteration can add draggable modules, reusable project files, WebView-backed web modules, weather/calendar integrations, templates, and scheduled re-rendering.
