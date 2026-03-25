import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import fs from "node:fs";
import path from "node:path";

const certDir = path.resolve(__dirname, "certs");
const certPath = path.resolve(certDir, "localhost.pem");
const keyPath = path.resolve(certDir, "localhost.key");
const hasDevCert = fs.existsSync(certPath) && fs.existsSync(keyPath);

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
    host: "localhost",
    https: hasDevCert
      ? {
          cert: fs.readFileSync(certPath),
          key: fs.readFileSync(keyPath)
        }
      : true,
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": {
        target: "https://localhost:7009",
        changeOrigin: true,
        secure: false // accept self-signed dev certificate
      }
    }
  }
});
