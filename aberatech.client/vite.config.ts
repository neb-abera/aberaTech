import * as child_process from "node:child_process"; // Required for running shell commands
import * as fs from "node:fs"; // Correct import for the file system module
import * as path from "node:path"; // Correct import for the path module
import { config as dotenvConfig } from "dotenv";

// Load .env file before anything else
dotenvConfig();

console.log("Available ENV variables:", process.env);
console.log("nebdebug: process.env.IN_DOCKER:", process.env.IN_DOCKER); // Should log 'true'

// Check if we are running inside Docker using the IN_DOCKER environment variable
console.log("nebdebug: process.env.IN_DOCKER:", process.env.IN_DOCKER);
const isDocker = process.env.IN_DOCKER?.toLowerCase() === "true" || false;
console.log("nebdebug: Is Docker:", isDocker);

// Define HTTPS config variable (will remain undefined during Docker builds)
let httpsConfig: { key: string; cert: string } | undefined;

if (!isDocker) {
  // Skip HTTPS configuration entirely for Docker builds
  const baseFolder =
    process.env.APPDATA !== undefined && process.env.APPDATA !== ""
      ? `${process.env.APPDATA}/ASP.NET/https` // Windows path for HTTPS certificates
      : `${process.env.HOME}/.aspnet/https`; // Unix/Linux path for HTTPS certificates

  const certificateName = "aberatech.client";
  const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
  const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

  // Create the base folder if it doesn't exist
  if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
  }

  // Check if HTTPS certificates exist, and generate them if they don't
  if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    const result = child_process.spawnSync(
      "dotnet",
      [
        "dev-certs",
        "https",
        "--export-path",
        certFilePath,
        "--format",
        "Pem",
        "--no-password",
      ],
      { stdio: "inherit" },
    );

    // If certificate creation fails, throw an error for non-Docker environments
    if (result.status !== 0) {
      throw new Error("Could not create development HTTPS certificate.");
    }
  }

  // Load HTTPS configuration once the certificates exist
  httpsConfig = {
    key: fs.readFileSync(keyFilePath, "utf-8"),
    cert: fs.readFileSync(certFilePath, "utf-8"),
  };
}

// Export the Vite configuration. A function rather than an object because the
// build runs twice: once for the browser bundle and once (--ssr) for the
// build-time renderer, whose single output file must land at a fixed name for
// tools/prerender.mjs to import.
export default ({ isSsrBuild = false } = {}) =>
  isSsrBuild
    ? {
        base: "/",
        build: {
          outDir: "dist-server",
          rollupOptions: {
            output: {
              entryFileNames: "[name].js",
            },
          },
        },
      }
    : clientConfig;

const clientConfig = {
  // Absolute, not './'. The server hands index.html back for every unmatched
  // path, so a relative asset reference is resolved against whatever URL the
  // visitor happened to arrive on: from /schedule it points at /assets and
  // works, and from /schedule/admin it points at /schedule/assets and 404s,
  // leaving a blank page. Every route below the first level would break the
  // same way.
  base: "/",
  build: {
    outDir: "dist",
    assetsDir: "assets",
    rollupOptions: {
      output: {
        entryFileNames: "assets/[name]-[hash].js",
        chunkFileNames: "assets/[name]-[hash].js",
        assetFileNames: "assets/[name]-[hash].[ext]",
      },
    },
  },
  server: {
    https: httpsConfig, // Use the generated HTTPS config if available
    port: 3000, // Specify development server port
  },
};
