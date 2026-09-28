// Lighthouse CI collection settings, shared by the pull request job and the
// nightly run (docs/lighthouse.md). LHCI_BASE_URL is the site under test:
// the production image on the compose network in a pull request, and
// https://abera.tech at night. LHCI_THROTTLING is devtools or simulate
// (run.sh collects both, into two directories).
const base = process.env.LHCI_BASE_URL;
if (!base) throw new Error("LHCI_BASE_URL is not set");
const throttlingMethod = process.env.LHCI_THROTTLING;
if (!["devtools", "simulate"].includes(throttlingMethod)) {
  throw new Error("LHCI_THROTTLING is not devtools or simulate");
}

const routes = require("./routes.json");

module.exports = {
  ci: {
    collect: {
      url: routes.map((route) => new URL(route, base).href),
      numberOfRuns: 5,
      chromePath: process.env.CHROME_PATH,
      settings: {
        // Lighthouse's default device: a mid-range phone on a slow 4G
        // link. The throttling constants are Lighthouse's defaults for
        // both methods.
        formFactor: "mobile",
        throttlingMethod,
        chromeFlags: "--headless=new --no-sandbox",
        // The thumbnails and the full page screenshot are 156 KB of a
        // 483 KB report. The server stores every report it is sent.
        disableFullPageScreenshot: true,
        skipAudits: ["screenshot-thumbnails"],
      },
    },
  },
};
