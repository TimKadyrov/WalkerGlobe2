# WalkerGlobe2

A 3D Earth visualization engine built on OpenGL (via OpenTK) and .NET 8.0. Renders satellites, ground stations, orbital elements, atmospheric effects, and geospatial data in real time. Supports standalone and WPF-hosted modes.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue)
![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-green)

## Features

- **Globe rendering** — ray-casted 3D Earth with day texture, optional night texture (city lights), and day/night terminator
- **Atmospheric scattering** — realistic atmosphere glow effect
- **Satellites** — per-satellite scale, highlighting, and color
- **Ground stations** — 3D markers (mast + dish + feed horn) with configurable transparency
- **Orbital polylines** — orbit traces in ECI frame
- **Ground overlays** — polylines, polygons, and shapefiles in geodetic coordinates (ECEF)
- **Coverage cones** — outlined and hollow cone visualizations
- **Sphere markers** — selection highlights with configurable radius and alpha
- **Line segments** — point-to-point links (e.g. satellite-to-ground LOS)
- **Star field** — billboard-based background stars, always visible regardless of zoom
- **Lat/lon grid** — zoom-dependent resolution (1° close, 10° far)
- **GSO arc** — geostationary orbit ring
- **HUD** — optional heads-up display with simulation time
- **Camera** — mouse orbit (left-drag rotate, right-drag zoom, scroll wheel)

## Project Structure

```
Source/
  WalkerGlobe2/              Core library + standalone entry point
    Api/                      Public interfaces (IGlobeViewer, GlobeWpfControl)
    Core/                     Math, geometry, coordinate transforms, shapefile parsing
    Scene/                    Renderable objects, cameras, globe, atmosphere
    Renderer/                 OpenGL 3.x backend (shaders, buffers, textures)
    Resources/                Globe textures (DayTexture.jpg, NightTexture.jpg)
    Program.cs                Standalone entry point
  WalkerGlobe2.WpfSample/    WPF demo app with Walker constellation
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK (Windows)
- OpenGL 3.3+ capable GPU

### Build

```bash
dotnet build
```

### Run Standalone

```bash
dotnet run --project Source/WalkerGlobe2
```

Launches a window with the globe. Requires `Resources/DayTexture.jpg` in the output directory.

### Run WPF Sample

```bash
dotnet run --project Source/WalkerGlobe2.WpfSample
```

Demo application with a 24-satellite Walker constellation (3 planes, 8 sats/plane, 1400 km altitude) and 4 ground stations (Paris, New York, Sydney, Tokyo). Includes playback controls, scene toggles, and satellite scale slider.

## API Usage

### IGlobeViewer (positions in km)

```csharp
using var viewer = new OpenGLGlobeViewer("path/to/DayTexture.jpg");

viewer.SetTime(timeSeconds);
viewer.UpdateSatellites(positionsKm, scales, highlighted);
viewer.UpdateGroundStations("gs", positionsKm, Color.Yellow, alpha: 0.3f);
viewer.SetSpacePolyline("orbit", orbitPointsKm, Color.Cyan);
viewer.SetGroundPolygons("coverage", polygonsLatLonRad, Color.White, Color.Blue);
viewer.ShowGrid = true;

viewer.Run(); // blocking
```

### WPF Hosted Mode

```xml
<globe:GlobeWpfControl x:Name="globeControl" />
```

```csharp
globeControl.GlobeReady += () =>
{
    globeControl.UpdateSatellites(positionsKm, Color.LightBlue, scales, highlighted);
    globeControl.UpdateGroundStations("gs", positionsKm, Color.Yellow);
    globeControl.ShowGrid = true;
    globeControl.ShowAtmosphere = true;
};
```

### Key API Methods

| Method | Description |
|--------|-------------|
| `SetTime(double)` | Set simulation time (drives Earth rotation) |
| `UpdateSatellites(...)` | Add/update satellite markers (ECI, km) |
| `UpdateGroundStations(...)` | Add/update ground station markers (ECEF, km) |
| `SetSpacePolyline(...)` | Add/update orbit trace (ECI, km) |
| `SetGroundPolyline(...)` | Add/update ground track (lat/lon radians) |
| `SetGroundPolygons(...)` | Add/update ground polygons (lat/lon radians) |
| `SetSphereMarkers(...)` | Add/update selection spheres (ECI, km) |
| `SetLineSegments(...)` | Add/update line pairs (ECI, km) |
| `Remove(string key)` | Remove a renderable by key |

### Visual Toggles

| Property | Default |
|----------|---------|
| `ShowGrid` | `false` |
| `ShowDayNight` | `false` |
| `ShowAtmosphere` | `false` |
| `ShowStars` | `true` |

## Dependencies

- [OpenTK.Graphics](https://www.nuget.org/packages/OpenTK.Graphics/) 4.9.4
- [OpenTK.Mathematics](https://www.nuget.org/packages/OpenTK.Mathematics/) 4.9.4
- [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common/) 10.0.5

## License

Apache License 2.0. See [LICENSE](LICENSE) for details.

Derived from [OpenGlobe](https://github.com/pjcozzi/OpenGlobe) by Patrick Cozzi and Deron Ohlarik (MIT License). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
