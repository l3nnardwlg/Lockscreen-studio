# Lockscreen Studio

A lightweight Windows lockscreen customization studio built with .NET 8 and WPF.

## Current MVP features

- Live 1920×1080 lockscreen preview
- Background image picker
- Modular canvas instead of hard-coded clock/date/text layout
- Addable clock, date, text, and web modules
- Drag modules freely across the lockscreen canvas
- Per-module inspector for X/Y, width, height, font size, text, and URL
- Web modules powered by Microsoft Edge WebView2
- HTTP/HTTPS URL validation for web modules
- PNG export
- WebView snapshot capture so web modules are included in exported PNGs
- Apply flow that renders the current design and opens Windows Lock Screen settings

## Run

Requirements: Windows 10/11, .NET 8 SDK, and the Microsoft Edge WebView2 Runtime.

```powershell
dotnet restore
dotnet run --project .\LockscreenStudio\LockscreenStudio.csproj
```

Or open `LockscreenStudio.sln` in Visual Studio 2022 and run the project.

## Using modules

1. Add a Clock, Date, Text, or Web module from the left sidebar.
2. Drag the module directly on the preview to position it.
3. Select it to edit exact position and dimensions in the inspector.
4. For web modules, enter any `http://` or `https://` URL and apply the properties.
5. Export the composition to PNG or render it for the Windows lockscreen apply flow.

## Architecture direction

The Windows lockscreen is treated as a rendered composition, while Lockscreen Studio itself is the interactive editor. The module model is now generic enough to add dedicated weather, calendar, Spotify/Now Playing, countdown, image, system-info, and plugin-backed module types next.
