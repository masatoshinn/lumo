# Lumo Engine

A C#/.NET game engine with real-time 3D rendering, Avalonia UI editor, scene management, physics, and game packaging.

## Features

- **Real-time 3D rendering** — OpenGL 3.3 via Silk.NET
- **Editor UI** — Avalonia with Home (project browser) and Work (full editor) screens
- **Scene management** — Entities, transforms, mesh renderers, lights, cameras
- **Project system** — Create, open, save projects as JSON
- **OBJ export** — Export scenes to `.obj` files
- **Tests** — xUnit test suite

## Projects

| Project | Description |
|---------|-------------|
| `Lumo.Engine` | Core engine (scene, rendering, physics, audio) |
| `Lumo.Editor` | Avalonia-based editor application |
| `Lumo.Runtime` | Game runtime player |
| `Lumo.Tools` | Asset/logo processing tools |
| `Lumo.Examples` | Example scenes |
| `Lumo.Tests` | Unit tests |

## Requirements

- .NET 11 SDK
- OpenGL 3.3 capable GPU

## Build

```bash
dotnet build LumoEngine.sln
```

## Test

```bash
dotnet test tests/Lumo.Tests
```

## Run Editor

```bash
dotnet run --project src/Lumo.Editor
```

## License

MIT — see [LICENSE](LICENSE).
