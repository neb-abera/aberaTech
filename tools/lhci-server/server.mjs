// The Lighthouse CI server behind the Lighthouse job and the nightly run.
// docs/lighthouse.md describes the deployment, the storage and the backup.
//
// Configuration comes from the environment so the container app holds the
// secrets as secret references:
//
//   LHCI_BASIC_AUTH_PASSWORD  required. The server refuses to start without
//                             it, so it is never served open. The workflows
//                             upload with it.
//   LHCI_ALLOWED_EMAILS       the Google accounts that may read the server
//                             in a browser, comma separated. gate.mjs.
//   LHCI_DATABASE_PATH        the SQLite file, on the Azure Files share.
//   PORT                      9001 unless set.
import { createServer } from "node:http";
import { createRequire } from "node:module";
import process from "node:process";
import { gate, parseAllowed } from "./gate.mjs";

const require = createRequire(import.meta.url);
const { createApp } = require("@lhci/server");
const sqlite3 = require("sqlite3");

const password = process.env.LHCI_BASIC_AUTH_PASSWORD ?? "";
if (password.length < 32) {
  process.stderr.write(
    "LHCI_BASIC_AUTH_PASSWORD is missing or shorter than 32 characters. Refusing to serve without basic auth.\n",
  );
  process.exit(1);
}

const databasePath = process.env.LHCI_DATABASE_PATH ?? "/data/lhci.db";

// 64 KB pages rather than SQLite's 4 KB. A report is about 315 KB, and on
// the Azure Files share every page written is a billed write. Ten reports
// took 272 write operations at 4 KB (2026-09-28). The page size lives in
// the file, so it is set once, before the first table or by a VACUUM.
await new Promise((resolve, reject) => {
  const db = new sqlite3.Database(databasePath, (error) => {
    if (error) reject(error);
  });
  const done = (error) =>
    db.close((closeError) => {
      const failure = error ?? closeError;
      if (failure) reject(failure);
      else resolve();
    });
  db.get("PRAGMA page_size", (error, row) => {
    if (error) return done(error);
    if (row.page_size === 65536) return done();
    db.exec("PRAGMA page_size = 65536; VACUUM;", done);
  });
});

// createApp rather than createServer, so gate.mjs decides who gets in
// instead of the built-in basic auth, whose challenge makes a browser show
// a password prompt.
const { app, storageMethod } = await createApp({
  logLevel: "verbose",
  storage: {
    storageMethod: "sql",
    sqlDialect: "sqlite",
    sqlDatabasePath: databasePath,
  },
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

const allow = gate({
  username: "lhci",
  password,
  allowed: parseAllowed(process.env.LHCI_ALLOWED_EMAILS),
});
const server = createServer((req, res) => allow(req, res, () => app(req, res)));
// As createServer in @lhci/server does: computing statistics can take longer
// than Node's default socket timeout.
server.on("connection", (socket) =>
  socket.setTimeout(20 * 60 * 1000, () => socket.end()),
);
const port = Number(process.env.PORT ?? 9001);
await new Promise((resolve, reject) => {
  server.once("error", reject);
  server.listen(port, resolve);
});

process.stdout.write(`lhci server listening on port ${port}\n`);

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, async () => {
    await Promise.all([
      new Promise((resolve) => server.close(resolve)),
      storageMethod.close(),
    ]);
    process.exit(0);
  });
}
