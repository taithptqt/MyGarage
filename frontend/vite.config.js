import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import basicSsl from "@vitejs/plugin-basic-ssl";
import path from "node:path";
import fs from "node:fs";

function resolveHttpsConfig(useHttps) {
  if (!useHttps) return false;

  const certFile = path.resolve(__dirname, ".cert/localhost.pem");
  const keyFile = path.resolve(__dirname, ".cert/localhost-key.pem");
  const hasMkcertPair = fs.existsSync(certFile) && fs.existsSync(keyFile);

  if (hasMkcertPair) {
    return {
      cert: fs.readFileSync(certFile),
      key: fs.readFileSync(keyFile)
    };
  }

  return true;
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const useHttps = (env.VITE_DEV_HTTPS || "true").toLowerCase() === "true";
  const httpsConfig = resolveHttpsConfig(useHttps);

  const plugins = [
    react({
      include: /\.(js|jsx|ts|tsx)(?:$|\?)/
    })
  ];

  if (useHttps && httpsConfig === true) {
    plugins.push(basicSsl());
  }

  return {
    plugins,
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
      https: httpsConfig,
      port: 5173,
      proxy: {
        "/api": {
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
      chunkSizeWarningLimit: 2000,
      reportCompressedSize: false,
      rollupOptions: {
        output: {
          manualChunks: {
            "react-vendor": ["react", "react-dom", "react-router-dom"],
            "chakra-vendor": [
              "@chakra-ui/react",
              "@chakra-ui/system",
              "@chakra-ui/icons",
              "@emotion/react",
              "@emotion/styled",
              "framer-motion"
            ],
            "chart-vendor": ["apexcharts", "react-apexcharts"],
            "realtime-vendor": ["@microsoft/signalr"]
          }
        }
      }
    }
  };
});
