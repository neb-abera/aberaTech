# aberaTech.Fitness

The military-athlete console behind `/fitness`: verified training data in,
sourced predictions out.

## What it does

- **Ingests** Garmin Connect exports (activities CSV for bulk history, single
  `.fit` files decoded with Garmin's own SDK), Hevy exports (free-tier CSV),
  and — when a Hevy Pro API key is configured — the official Hevy REST API.
  Every import is idempotent: records land keyed by `(source, external id)`.
- **Analyses**: monthly aerobic trend as HR-normalized pace (the field version
  of a monthly aerobic-threshold test), weekly training dose against the plan,
  estimated 1RM trends (Epley, Brzycki cross-check), and rule-based highlights
  that carry their evidence — regressions included.
- **Predicts**: VDOT trajectory under an adjustable dose (weekly hours ×
  compliance), with bodyweight as a factor (VDOT is per-kilogram), race-time
  checkpoints at 6/12/18/24 months, goal arrival dates, and the inverse — name
  a goal and a deadline, get the required weekly dose.
- **Cites**: every model carries a discipline-matched citation (Daniels,
  Banister/Busso, Seiler, San-Millán & Brooks, Johnston/Kuenzle/Paikowski,
  Cureton & Sparling, Epley, Brzycki, Coggan). `/api/fitness/citations` serves
  the registry; the UI renders it under Sources.

## The model, in one paragraph

Race equivalency is Daniels VDOT (Daniels & Gilbert 1979). The trajectory is
the constant-dose solution of the Banister impulse-response family:
`V(t) = C − (C − V0)·e^(−kt)` with `C = 38 + 1.6 × effective weekly hours` and
`k = 0.0676/month`, calibrated so the athlete's plan dose reproduces documented
aerobic-deficiency recoveries. Bodyweight scales VDOT by the inverse mass ratio
(clamped to ±10%; fat-mass assumption). The inverse solver inverts the same
equation for `C`, so forward and inverse can never disagree. A new time trial
recalibrates `StartVdot` in settings — predictions are only ever as good as the
last measured anchor.

## Configuration

Everything fails closed: with any of these missing, `/api/fitness/*` is never
mapped and `/api/fitness/me` answers `configured: false`.

| Setting | Meaning |
| --- | --- |
| `ConnectionStrings:Fitness` | Postgres database (its own database on the shared server). |
| `Fitness:AllowedEmails` | The Google accounts allowed in — the athlete. |
| `Admin:*` (existing) | Provides the Google sign-in schemes; fitness reuses them. |
| `Fitness:HevyApiKey` | Optional. Hevy Pro API key; enables `/api/fitness/sync/hevy`. A container app secret, never appsettings. |
| `Database:UseEntraAuth` | Same switch the scheduling database uses; applies here too. |

## Costs

Hosting rides the existing container app and Postgres server: no new
infrastructure. The only optional cost is Hevy Pro (~$24/year) for its API;
the CSV upload path keeps the free tier fully functional. Garmin's official
API is enterprise-only, so Garmin data arrives by export upload — by design,
no credentials for any third-party service are stored anywhere in this
feature.
