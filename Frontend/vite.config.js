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
      "@context": path.resolve(__dirname, "src/shared/core/context"),
      "@app": path.resolve(__dirname, "src/app"),
      "@features": path.resolve(__dirname, "src/features"),
      "@shared": path.resolve(__dirname, "src/shared"),
      "@horizon": path.resolve(__dirname, "src/horizon"),
      "@legacy": path.resolve(__dirname, "src/legacy")
    }
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "https://localhost:7009",
        changeOrigin: true,
        secure: false // accept self-signed dev certificate
      },
      "/odata": {
        target: "https://localhost:7009",
        changeOrigin: true,
        secure: false // accept self-signed dev certificate
      },
      "/uploads": {
        target: "https://localhost:7009",
        changeOrigin: true,
        secure: false
      },
      "/hubs": {
        target: "https://localhost:7009",
        changeOrigin: true,
        secure: false,
        ws: true
      }
    }
  },
  build: {
    chunkSizeWarningLimit: 1000,
    rollupOptions: {
      output: {
        manualChunks: {
          "vendor-react": ["react", "react-dom", "react-router-dom"],
          "vendor-chakra": [
            "@chakra-ui/react",
            "@chakra-ui/system",
            "@chakra-ui/theme-tools",
            "@emotion/react",
            "@emotion/styled"
          ],
          "vendor-charts": ["apexcharts", "react-apexcharts"],
          "vendor-icons": ["react-icons", "lucide-react", "@chakra-ui/icons"]
        }
      }
    }
  }
});
