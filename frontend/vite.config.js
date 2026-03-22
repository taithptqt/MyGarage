import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [
    react({
      include: /\.(js|jsx|ts|tsx)(?:$|\?)/
    })
  ],
  resolve: {
    alias: {
      assets: path.resolve(__dirname, "src/horizon/assets"),
      components: path.resolve(__dirname, "src/horizon/components"),
      contexts: path.resolve(__dirname, "src/horizon/contexts"),
      layouts: path.resolve(__dirname, "src/horizon/layouts"),
      theme: path.resolve(__dirname, "src/horizon/theme"),
      variables: path.resolve(__dirname, "src/horizon/variables"),
      views: path.resolve(__dirname, "src/horizon/views"),
      routes: path.resolve(__dirname, "src/horizon/routes.jsx"),
      "@context": path.resolve(__dirname, "src/context")
    }
  },
  server: {
    https: true,
    port: 5173,
    proxy: {
      "/api": {
        target: "https://localhost:7009",
        changeOrigin: true,
        secure: false // accept self-signed dev certificate
      }
    }
  }
});
