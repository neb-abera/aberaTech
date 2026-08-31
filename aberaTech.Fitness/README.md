# aberaTech.Fitness

The military-athlete console behind `/fitness`: verified training data in,
sourced predictions out.

## What it does

- **Ingests** whatever these services hand out, as they hand it out. One
  route, `POST /api/fitness/import`, sniffs the bytes: Garmin's "Export Your
  Data" archive (walked for its activity summaries and the originals nested in
  a second zip), a single `.fit` decoded with Garmin's own SDK, the Connect
  website's activities CSV, or a Hevy export CSV — and, when a Hevy Pro API key
  is configured, the official Hevy REST API. Content decides, never the file
  name. Every import is idempotent: records land keyed by
  `(source, external id)`, and the archive keys on Garmin's own activity id, so
  re-importing it — or a later archive that overlaps it — updates rather than
  duplicates. Two files describing one session reconcile: see below.
- **Reconciles** the two files Garmin offers. The export carries a real UTC
  clock; the Connect website's CSV carries a wall clock and no zone, and an
  athlete who has moved has no single zone that would fit their history. So the
  same run read from both is matched on what the session was — sport, duration
  to the second, distance to 25 m, within any real UTC offset — and becomes one
  row. The export wins: a CSV row it already covers is not stored, and one
  imported earlier is replaced. Without that, uploading both doubles the weekly
  volume chart, quietly and in the flattering direction.
- **Analyses**: monthly aerobic trend as HR-normalized pace (the field version
  of a monthly aerobic-threshold test), weekly training dose against the plan,
  estimated 1RM trends (Epley, Brzycki cross-check), and rule-based highlights
  that carry their evidence — regressions included.
- **Predicts**: VDOT trajectory under an adjustable dose (weekly hours ×
  compliance), with bodyweight as a factor (VDOT is per-kilogram), race-time
  checkpoints at 6/12/18/24 months, goal arrival dates, and the inverse — name
  a goal and a deadline, get the required weekly dose. Altitude is asked twice,
  because a posting moves an athlete without moving their history: the anchor
  and the lifetime best are scored where they were run, the goals where they
  will be. Leaving the past one blank means "same place" and reproduces the
  single-altitude behaviour exactly.
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

## Reading an archive safely

An upload is a zip from outside, so `Ingest/Import.cs` treats it as hostile:
nothing is ever written to disk, so there is no path to traverse; members are
read as streams and counted as they are read, because a zip's declared sizes
are written by whoever made it; and the walk stops at 20,000 members, 64 MB for
any one of them, 512 MB across the whole archive, and two levels of nesting —
one more than Garmin itself uses. The request body is capped at 100 MB, which
is Kestrel's 30 MB default raised deliberately for this one route.
