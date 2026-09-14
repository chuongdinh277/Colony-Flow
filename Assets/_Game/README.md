# Colony Flow framework

## Quick start

1. In Unity, run `Colony Flow > Create Demo Content` once.
2. Open `Assets/_Game/Scenes/Gameplay.unity` and press Play.
3. Click an available colored colony tile below the board.
4. Open `Colony Flow > Image To Level` to convert a source image into a level.

## Runtime pipeline

`LevelData -> PixelBoard -> ColonyTileBoard -> ColonyTray -> ColonyController -> AntRouteService -> AntAgent`

Ant movement is four-directional. Every route starts at the bottom border spawn, follows the rectangular border to the border point nearest its target, then enters the cleared grid and reaches an exposed pixel. A pixel is reserved before an ant is launched.

## Image tool

The converter samples an image at the requested grid resolution, ignores pixels below the alpha threshold, quantizes remaining pixels against `PixelPalette`, and generates colony counts that exactly match the generated pixels. Use the custom `Validate Level` button on a `LevelData` asset after manual editing.

## Odin level designer

Open `Colony Flow > Level Designer` (`Ctrl+Shift+L`). The unified Odin window contains Image Import, Pixel Painter, four draggable Colony Stack columns, and Validate & Play pages. Runtime data remains plain Unity `ScriptableObject` data; Odin is editor-only.

## Production extension points

- Replace runtime square sprites with art prefabs in `PixelBoard`, `ColonyTileBoard`, and `LevelController`.
- Add tile text/UI views without changing the domain models.
- Add explicit `coveredBy` tile IDs to author layered puzzles.
- Keep logical four-direction routes and apply visual offsets only in the ant renderer.
