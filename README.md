# Adamantium Engine

A graphics engine for .NET on its own Vulkan bindings: a Vulkan renderer, effects compiled to SPIR-V at build time,
fonts with MSDF atlases, image codecs, 3D model import and an entity-component system. It is the foundation of
[Adamantium UI](https://github.com/AdamantiumStudio/AdamantiumUI), where a 3D scene and the interface around it are
drawn by one renderer in one process.

> **Alpha.** Windows only for now, and the API will change between releases.

## Packages

| Package | What it is |
|---|---|
| `Adamantium.Core` | Disposable and named objects, observable collections, commands, events, data buffers, dependency injection, type parsing |
| `Adamantium.Mathematics` | Vectors, matrices, quaternions, colors, bounding volumes, polygon triangulation and fill rules |
| `Adamantium.Fonts` | TrueType, OpenType and CFF: outlines, metrics, kerning, OpenType features; MSDF glyph atlases |
| `Adamantium.Imaging` | PNG and APNG, JPEG, GIF, BMP, TGA, TIFF, ICO, DDS; mipmaps, format conversion, palette quantizers |
| `Adamantium.Graphics.Core` | Devices, presenters, buffers, textures, vertex layouts, meshes, the effects framework |
| `Adamantium.Graphics` | The Vulkan renderer: device, swap chains, memory allocation, pipelines, effects, textures, text |
| `Adamantium.EffectsCompiler` | Compiles effects (`.fx`) to SPIR-V with Slang, with the reflection data the engine binds them by |
| `Adamantium.FX` | Built-in effects: basic, font, sprite, Forward+ lighting, full-screen quad |
| `Adamantium.Engine.Compiler` | Imports 3D models: Collada, Wavefront OBJ, 3DS |
| `Adamantium.ECS` | Entities and components: cameras, colliders, animation, meshes |
| `Adamantium.Multiverse` | Universes, the outputs they render to, windowing platforms, input |
| `Adamantium.Engine` | Forward+ rendering, scene managers, cameras, selection, editing tools |
| `Adamantium.ProceduralGeometry` | Rectangles, ellipses, arcs, polygons, lines, cubes, spheres, cylinders, cones, capsules, grids |
| `Adamantium.MVVM` | View models with validation, sync and async commands |
| `Adamantium.Win32` | Windows, messages, system metrics, cursors, Shell dialogs |
| `Adamantium.MacOS` | Application, windows and cursors through a native app wrapper (not yet verified, see below) |
| `Adamantium.XInput` | XInput gamepads |
| `Adamantium.GameInput` | Microsoft GameInput gamepads, with the native shim they need |
| `Adamantium.Engine.Generators` | Source generators: a typed class for every `.fx` in the project, a constant for every content asset |
| `Adamantium.MVVM.Generators` | Source generator for `Adamantium.MVVM`: bindable properties and commands from attributes |

All packages share one version and are released together.

## Requirements

- **.NET 10.**
- **Windows 10 or 11, x64.** macOS and Linux are planned; the macOS code exists but has not been built and run
  recently, so it is not claimed.
- **A GPU and driver with Vulkan 1.4**, `VK_EXT_shader_object` and `VK_EXT_descriptor_heap`, and the `shaderInt64`,
  `shaderDrawParameters` and `geometryShader` features. The descriptor heap is recent, and it sets the floor. By the
  reports on [vulkan.gpuinfo.org](https://vulkan.gpuinfo.org):
  - **NVIDIA:** Turing or newer - GeForce GTX 16 and RTX 20 series and later, Quadro RTX and T series, RTX A, Ada and
    Blackwell. Ampere and newer report it from driver 582, Turing from driver 595.
  - **AMD:** RDNA 3 or newer - Radeon RX 7000 and RX 9000 series, Radeon PRO W7000, and Radeon 740M, 760M, 780M and
    newer integrated graphics, with a current driver.
  - **Intel:** no Windows driver reports the descriptor heap yet.

  The engine is developed and tested on an NVIDIA Quadro RTX 4000 with NVIDIA's
  [Vulkan developer driver](https://developer.nvidia.com/vulkan-driver); other GPUs have not been tested.
- **No Vulkan SDK.** The Slang shader compiler ships in the
  [`Adamantium.Vulkan.Slang`](https://www.nuget.org/packages/Adamantium.Vulkan.Slang) package.

## Getting started

```
dotnet add package Adamantium.Engine --prerelease
```

`Adamantium.Engine` brings in most of the others as dependencies. Add the input and platform packages you need
beside it.

### Your own effects

Every `.fx` file in a project becomes a class at build time: `Effects/Glow.fx` in a project with the root namespace
`MyApp` gives `MyApp.Effects.Glow`, derived from `Effect`. The generator needs to see the files and to know where the
Slang compiler is:

```xml
<ItemGroup>
  <PackageReference Include="Adamantium.Engine.Generators" Version="0.1.0-alpha" PrivateAssets="all" />
  <PackageReference Include="Adamantium.Vulkan.Slang" Version="1.0.11" GeneratePathProperty="true" PrivateAssets="all" />
  <AdditionalFiles Include="**\*.fx;**\*.fxh" />
  <CompilerVisibleProperty Include="RootNamespace;ProjectDir;AdamantiumSlangPath" />
</ItemGroup>

<PropertyGroup>
  <AdamantiumSlangPath>$(PKGAdamantium_Vulkan_Slang)</AdamantiumSlangPath>
</PropertyGroup>
```

A shader that does not compile fails the build with the compiler's message.

## Building from source

```
git clone https://github.com/AdamantiumStudio/AdamantiumEngine.git
cd AdamantiumEngine
dotnet build AdamantiumEngine.sln -c Debug
```

Everything the build needs comes from nuget.org. The tests are in `AdamantiumEngine.Tests.sln`;
`Adamantium.Engine.GraphicsTests` needs a Vulkan device that meets the requirements above.

## Related repositories

- [AdamantiumUI](https://github.com/AdamantiumStudio/AdamantiumUI): the UI framework built on this engine.
- [AdamantiumVulkan](https://github.com/AdamantiumStudio/AdamantiumVulkan): the Vulkan, Slang and SPIR-V bindings.
- [QuantumBinding](https://github.com/AdamantiumStudio/QuantumBinding): the generator those bindings are made with.

## License

[Apache-2.0](https://github.com/AdamantiumStudio/AdamantiumEngine/blob/master/LICENSE). Parts of the engine are
derived from third-party work under MIT and ISC; their notices are in the file headers and in
[THIRD-PARTY-NOTICES.md](https://github.com/AdamantiumStudio/AdamantiumEngine/blob/master/THIRD-PARTY-NOTICES.md).
