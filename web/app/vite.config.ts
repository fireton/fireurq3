import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@fireurq/core": fileURLToPath(new URL("../packages/core/src/index.ts", import.meta.url)),
      "@fireurq/player": fileURLToPath(new URL("../packages/player/src/index.ts", import.meta.url))
    }
  },
  optimizeDeps: {
    include: ["pixi.js"]
  },
  server: {
    host: "0.0.0.0",
    port: 5173
  }
});
