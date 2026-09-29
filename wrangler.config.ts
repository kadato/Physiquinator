import { defineWranglerConfig } from "wrangler/experimental-config";

export default defineWranglerConfig({
  // dotnet publish output consumed by .github/workflows/deploy-web.yml.
  assetsDirectory: "./artifacts/wasm-publish/wwwroot"
});
