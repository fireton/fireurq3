# Skin Compatibility Mapping

## Legacy XML Input

Supported root layout:

- `<skin>`
- `<screen width height fullscreen>`
- `<resources>` with `texture`, `font`, `buttonframe`
- `<main>` with `textpane`, `menus`

## Canonical Model Mapping

- `screen.width` -> `SkinDefinition.ScreenWidth` (default `800`)
- `screen.height` -> `SkinDefinition.ScreenHeight` (default `600`)
- `screen.fullscreen` -> `SkinDefinition.FullScreen` (default `false`)
- `resources.texture` -> `SkinDefinition.Textures` (case-insensitive name)
- `resources.font` -> `SkinDefinition.Fonts` (case-insensitive name)
- `resources.buttonframe` -> `SkinDefinition.ButtonFrames`
- `main.textpane` -> `SkinDefinition.TextPane`
- `main.menus.selectioncolor` -> `SkinDefinition.MenuStyle.SelectionColor`

## Fallback Rules

1. Quest `skin.xml` if present.
2. Quest `skin.json` if present.
3. Built-in default skin (`Assets/Skins/default/skin.xml`).

Missing values fall back to defaults.
