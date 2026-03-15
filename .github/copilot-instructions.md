# Copilot Review Instructions -- TaintedGrailMinimap

This is a BepInEx mod for **Tainted Grail: Conquest** that adds a circular minimap HUD overlay. It runs as a Unity `MonoBehaviour` attached to a persistent `GameObject`.

## Architecture

| File | Responsibility |
|---|---|
| `Plugin.cs` | BepInEx entry point. Creates the `MinimapBehaviour` GameObject. |
| `MinimapConfig.cs` | All user-facing configuration (BepInEx `ConfigEntry` fields). |
| `MinimapBehaviour.cs` | Core MonoBehaviour: lifecycle, input, UI creation, per-frame updates. |
| `MinimapRenderer.cs` | Pure static math: coordinate transforms, UV rect computation, marker positioning. |
| `MinimapTextureFactory.cs` | Static procedural texture/sprite generation (circle masks, arrow, marker dots). |

## Key Constraints

- **No custom shaders.** The game's map textures loaded via Addressables are not CPU-readable (`GetPixels` fails). All rendering uses Unity's built-in `RawImage.uvRect` for map viewport and a `Mask` component with a circle sprite for clipping.
- **No Harmony patches.** The mod is entirely additive -- it reads game state through the public API (`World.Services`, `Hero.Current`, `CommonReferences`, etc.) without patching any game methods.
- **Target framework is `netstandard2.1`.** No C# 10+ features. The game uses Unity's Mono runtime.
- **Reference DLLs in `lib/` are proprietary** (game assemblies). They are gitignored and must never be committed. The `dist/` folder holds the pre-built release DLL.
- **`_mapAspect` is forced to `1f`** to fix stretching on non-square map textures. Do not reintroduce dynamic aspect ratio computation from texture dimensions.

## Review Focus Areas

### Correctness
- Null checks before accessing game services (`World.Services.Get<T>()`, `Hero.Current`, `CommonReferences.Get`). These can all return null during loading screens and scene transitions.
- The `_sceneGeneration` counter prevents stale async sprite load callbacks from corrupting state. Ensure any new async operations follow this pattern.
- `TryInitialize` wraps `MapData.byScene.TryGetValue` in a try/catch for `NullReferenceException` because the backing data structure may not be initialized yet. This is intentional.

### Performance
- `LateUpdate` runs every frame. Avoid allocations (no `new` for reference types, no LINQ, no string concatenation in the hot path).
- `UpdateQuestMarkers` uses a pre-allocated `List<QuestMarker>` buffer. New marker types should follow this pattern.
- Texture generation methods are cached (static fields). Ensure textures are only created once.

### Code Organization
- Texture/sprite generation belongs in `MinimapTextureFactory`, not `MinimapBehaviour`.
- Coordinate math and marker positioning belongs in `MinimapRenderer`, not `MinimapBehaviour`.
- UI element creation should use the `SetFillParent`/`SetCentered` helpers to avoid repeated RectTransform boilerplate.
- Magic numbers should be named constants at the top of the relevant class.

### Type Ambiguities
- `Compass` is ambiguous between `Awaken.TG.Main.Maps.Compasses.Compass` and `UnityEngine.Compass`. Always fully qualify it.
- `FontStyles` is ambiguous between `TMPro.FontStyles` and `UnityEngine.TextCore.Text.FontStyles`. Always qualify as `TMPro.FontStyles`.

### Config
- New user-facing settings go in `MinimapConfig.cs` using BepInEx `ConfigEntry<T>`.
- Use `AcceptableValueRange<T>` for numeric configs to enforce bounds.
- Config values are read every frame (hot-reload friendly). Do not cache them in fields unless there's a performance reason.

### Testing
- There are no automated tests. The mod must be tested in-game.
- Build with `dotnet build -c Release`. The output DLL goes to `bin/Release/netstandard2.1/`.
- Deploy by copying the DLL to `BepInEx/plugins/` in the game directory.
