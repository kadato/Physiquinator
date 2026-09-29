import { defineConfig } from "cf/config";

export default defineConfig({
  worker: {
    name: "physiquinator",
    compatibilityDate: "2026-09-29",
    assets: {
      // Blazor WASM is an SPA: serve index.html for unknown paths so the
      // client router renders its own NotFound UI. Mirrors the `_redirects`
      // fallback (`/* /index.html 200`) at the routing layer.
      notFoundHandling: "single-page-application"
    }
  }
});
