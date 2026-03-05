# Skin JSON Schema v1 (Draft)

## Purpose

Canonical skin representation for future evolution while preserving legacy XML runtime support.

## Top-Level

- `version` (string, optional, default `"1"`)
- `screen` (object)
- `textPane` (object)

## `screen`

- `width` (int, default `800`)
- `height` (int, default `600`)
- `fullscreen` (bool, default `false`)

## `textPane`

- `left` (int, default `20`)
- `top` (int, default `55`)
- `width` (int, default `760`)
- `height` (int, default `525`)
- `buttonAlign` (int, default `1`)

## Notes

- v1 loader currently supports `screen` and `textPane` subset.
- Extended resources/styles are planned next.
