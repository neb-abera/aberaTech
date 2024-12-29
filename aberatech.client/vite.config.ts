import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const isDocker = process.env.IN_DOCKER === 'true';

let httpsConfig: { key: string; cert: string } | undefined = undefined;

if (!isDocker) {
  const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== '' ? `${env.APPDATA}/ASP.NET/https` : `${env.HOME}/.aspnet/https`;

  const certificateName = 'aberatech.client';
  const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
  const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

  if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
  }

  if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    if (
      0 !==
      child_process.spawnSync(
        'dotnet',
        ['dev-certs', 'https', '--export-path', certFilePath, '--format', 'Pem', '--no-password'],
        { stdio: 'inherit' }
      ).status
    ) {
      throw new Error('Could not create development HTTPS certificate.');
    }
  }

  httpsConfig = {
    key: fs.readFileSync(keyFilePath).toString(),
    cert: fs.readFileSync(certFilePath).toString()
  };
} else {
  // Docker-specific configuration: Use cert paths from environment variables
  if (process.env.CERT_KEY && process.env.CERT_CRT) {
    httpsConfig = {
      key: fs.readFileSync(process.env.CERT_KEY).toString(),
      cert: fs.readFileSync(process.env.CERT_CRT).toString()
    };
  }
}

const target = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(';')[0]
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
    https: httpsConfig // Unified HTTPS configuration
  }
});
