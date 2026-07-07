import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const root = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  resolve: {
    alias: {
      "@dimforge/rapier3d-compat": resolve(
        root,
        "node_modules/@dimforge/rapier3d-compat/rapier.es.js",
      ),
    },
  },
  test: {
    environment: "node",
  },
});
