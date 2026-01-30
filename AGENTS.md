# Repository Guidelines

## Project Structure & Module Organization
- `Assets/Landscape/` holds project content: `Materials/`, `Scenes/`, `Scripts/`, `Settings/`, `ShaderLibrary/`, `Shaders/`, `Textures/`.
- `Assets/TextMesh Pro/` contains TMP assets imported by Unity.
- `Packages/manifest.json` defines Unity packages (including URP).
- `ProjectSettings/` stores editor and project configuration; keep changes intentional.
- `Library/`, `Temp/`, and `Logs/` are local Unity caches and should not be edited by hand.

## Build, Test, and Development Commands
- Unity Editor version: `6000.1.11f1` (see `ProjectSettings/ProjectVersion.txt`).
- Run locally: open the project in Unity Hub, load `Assets/Landscape/Scenes/SampleScene.unity`, then press Play.
- Build: use `File > Build Settings` in the Editor (no repo-specific build scripts yet).
- Tests (CLI, optional):
  `"<UnityEditor>" -projectPath . -batchmode -runTests -testPlatform editmode -logFile test.log`

## Coding Style & Naming Conventions
- C# style follows Unity defaults: 4-space indents, Allman braces, `PascalCase` for types/methods, `camelCase` fields, and `_camelCase` private fields.
- Keep class names and filenames aligned (e.g., `SkyRenderer.cs`).
- Shaders live under `Assets/Landscape/Shaders/` (e.g., `Sky/Skybox.shader`, `Sky/Sky.compute`); shared HLSL goes in `ShaderLibrary/`.
- Always commit `.meta` files alongside assets.

## Testing Guidelines
- Unity Test Framework is installed; add tests under `Assets/Tests` or `Assets/Landscape/Tests` using `*Tests.cs` naming.
- Run tests via `Window > General > Test Runner` or the CLI command above.
- No coverage targets are defined; keep tests focused on rendering helpers and math utilities.

## Commit & Pull Request Guidelines
- Git history only shows `Init project`, so no formal convention exists yet. Use short, imperative summaries (e.g., `Add sky LUT cache`).
- PRs should include: purpose, affected scenes/assets, steps to verify, and screenshots/GIFs for visual changes.
- Note any package or ProjectSettings changes, and verify Git LFS is active for new binary assets.

## Asset & LFS Notes
- `.gitattributes` routes common asset formats (textures, models, audio, video, fonts, `.unitypackage`) through Git LFS.
- If LFS is not installed, set it up before adding large binaries to avoid bloated commits.
