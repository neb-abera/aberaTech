// The Lighthouse CI server behind the Lighthouse job and the nightly run.
// docs/lighthouse.md describes the deployment, the storage and the backup.
//
// Configuration comes from the environment so the container app holds the
// one secret as a secret reference:
//
//   LHCI_BASIC_AUTH_PASSWORD  required. The server refuses to start without
//                             it, so it is never served open.
//   LHCI_DATABASE_PATH        the SQLite file, on the Azure Files share.
//   PORT                      9001 unless set.
import { createRequire } from "node:module";
import process from "node:process";

const require = createRequire(import.meta.url);
const { createServer } = require("@lhci/server");

const password = process.env.LHCI_BASIC_AUTH_PASSWORD ?? "";
if (password.length < 32) {
  process.stderr.write(
    "LHCI_BASIC_AUTH_PASSWORD is missing or shorter than 32 characters. Refusing to serve without basic auth.\n",
  );
  process.exit(1);
}

const server = await createServer({
  port: Number(process.env.PORT ?? 9001),
  logLevel: "verbose",
  storage: {
    storageMethod: "sql",
    sqlDialect: "sqlite",
    sqlDatabasePath: process.env.LHCI_DATABASE_PATH ?? "/data/lhci.db",
  },
  basicAuth: { username: "lhci", password },
  // Pull request and master builds are kept 14 days. A pull request
  // compares against the newest master build. Production builds, two a
  // night (DevTools and simulated throttling), are kept 90 days. docs/lighthouse.md has the storage arithmetic.
  // The jobs run only while a replica is up, which is whenever something
  // uploads.
  deleteOldBuildsCron: [
    {
      schedule: "*/10 * * * *",
      maxAgeInDays: 14,
      skipBranches: ["production", "production-simulated"],
    },
    {
      schedule: "*/10 * * * *",
      maxAgeInDays: 90,
      onlyBranches: ["production", "production-simulated"],
    },
  ],
});

process.stdout.write(`lhci server listening on port ${server.port}\n`);

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, async () => {
    await server.close();
    process.exit(0);
  });
}
