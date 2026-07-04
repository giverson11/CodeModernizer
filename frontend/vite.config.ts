import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Dev: proxies /api to the ASP.NET backend (see launchSettings.json).
// Build: emits straight into the API's wwwroot so the backend serves the SPA.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": "http://localhost:5210",
    },
  },
  build: {
    outDir: "../src/CodeModernizer.Api/wwwroot",
    emptyOutDir: true,
  },
});
