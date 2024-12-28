import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';

// Determine if the build is running in Docker
const isDocker = process.env.IN_DOCKER === 'true';

// Default target for the API proxy setup (update the target URL if necessary)
const target = process.env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${process.env.ASPNETCORE_HTTPS_PORT}`
  : process.env.ASPNETCORE_URLS
    ? process.env.ASPNETCORE_URLS.split(';')[0]
    : 'https://localhost:7083';

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [plugin()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  server: {
    proxy: {
      '^/weatherforecast': {
        target,
        secure: false
      }
    },
    port: 63813,
    https: !isDocker
      ? {
          // Only load certificates if not in Docker
          key: './path/to/key.pem',
          cert: './path/to/cert.pem'
        }
      : undefined // Use undefined instead of false to avoid TypeScript warnings
  }
});
