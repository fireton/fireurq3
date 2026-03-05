# Virtual Viewport Policy

## Model

- Game-space: fixed virtual coordinates from skin screen size (default `800x600`).
- View-space: real window size, resizable by user.

## Mapping

- Scale: `min(viewW / gameW, viewH / gameH)`.
- Offset: center letterboxed viewport.
- Bars: unused areas filled as letterbox.

## Input

- Mouse coordinates are inverse-mapped from view-space to virtual game-space.
- Hit-tests use virtual-space rectangles only.
- Clicks in letterbox bars are ignored.

## Guarantees

- Quest author coordinates remain stable regardless of window size.
- Resize does not change gameplay semantics.
