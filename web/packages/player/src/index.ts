export * from "./source.js";

export const playerMigrationStatus = {
  summary:
    "The player package now owns quest source resolution and browser-side quest loading for URL, library, and local file flows.",
  nextSteps: [
    "Add session/runtime bridge on top of @fireurq/core",
    "Introduce the graphical renderer contract for transcript and buttons",
    "Connect quest loading to the PixiJS player scene"
  ]
} as const;
