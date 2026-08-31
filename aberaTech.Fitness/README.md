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
- **Predicts**: a VDOT trajectory under a training week stated by intensity,
  with an 80% band around it, race-time checkpoints over whatever distances
  and horizons are asked for, and per-goal probabilities.
- **Prescribes**: name any distance, any time and any date, and get back the
  week it needs — hours by zone — or the constraint that makes it impossible.
- **Cites**: every model carries a discipline-matched citation (Daniels,
  Banister/Busso, Seiler, San-Millán & Brooks, Johnston/Kuenzle/Paikowski,
  Cureton & Sparling, Rønnestad & Mujika, Gabbett, Coggan, Epley, Brzycki,
  Seber & Wild). `/api/fitness/citations` serves the registry; the UI renders
  it under Sources, and every number links to the step that produced it.

## The model

Everything below is computed, not asserted, and every result the API returns
ships a trace of the arithmetic with this athlete's numbers substituted.

### Race equivalency

Daniels VDOT (Daniels & Gilbert 1979): a race time implies an oxygen-cost
score, and equal scores are equivalent performances. `Vdot.SpeedElasticity`
differentiates the same equations to get d(ln VDOT)/d(ln speed) at the
athlete's own pace — about 1.1, not 1 — which is what turns a measured pace
improvement into a fitness one.

### What a training week buys

The dose is a vector of hours by intensity, and each zone saturates:

```
C(h) = 38 + r · Σ bᵢ·sᵢ·(1 − e^(−hᵢ/sᵢ))
```

| Zone | bᵢ (VDOT per hour, first hour) | sᵢ (saturation, h/week) | strain per hour |
| --- | --- | --- | --- |
| Easy | 1.960 | 14.0 | 1.0 |
| Threshold | 2.856 | 1.2 | 2.5 |
| Interval | 3.373 | 0.6 | 4.5 |
| Strength | 1.078 | 1.5 | 1.5 |

The `bᵢ` were fixed by two conditions, both asserted in `DoseResponseTests`:
the optimal split of an eight-hour week comes out at 80% easy — Seiler's
observed distribution, reproduced rather than assumed — and over 4–12 h/week
the surface agrees within ~1.5 VDOT with the linear `C = 38 + 1.6h` ceiling
this app previously used, which was calibrated against documented
aerobic-deficiency recoveries. Past that range it deliberately diverges: a
straight line has no ceiling, and extrapolating one is what let this
calculator once answer "45.8 hours a week" for a five-mile time no human has
run. `r` is the athlete's responsiveness, fitted from their own history.

### How the hours get split

Maximising `C(h)` subject to `Σhᵢ = H` and `Σcᵢhᵢ ≤ Sₘₐₓ` is a constrained
optimum, solved from the Lagrangian conditions rather than by search:

```
r·bᵢ·e^(−hᵢ/sᵢ) = λ + μ·cᵢ    ⟹    hᵢ = sᵢ·ln(r·bᵢ / (λ + μ·cᵢ))
```

so an inner bisection on λ meets the hours constraint and an outer one on μ
meets the recovery budget. λ is the shadow price of an hour — what one more
hour a week is worth in VDOT — and μ the price of recovery; both are reported,
because "where should my next hour go" is answered by comparing them.

### The trajectory

An ODE rather than a formula, so a ramping plan is as computable as a steady
one:

```
dV/dt = k(V)·(C(h(t)) − V)
```

integrated by fourth-order Runge-Kutta at ~1.5-day steps. Under a constant
dose it reproduces the closed-form exponential to eight decimals, which is the
test that keeps the numerics honest. `k(V)` carries the retraining fast lane:
2.5× the de-novo rate at the starting fitness, tapering to 1× at the
age-adjusted lifetime peak. Plans ramp from the week the athlete is actually
training — read out of their log, each session placed by its own average pace
against their own bands — at 8% a week, so no date assumes a jump nobody
could start.

### Fitting it to the athlete

Three parameters — starting VDOT, approach rate `k`, responsiveness `r` — by
penalised nonlinear least squares (Levenberg-Marquardt, central-difference
Jacobian over the ODE solve). The penalty is a Gaussian prior on each
parameter at its literature value, so four noisy months cannot conclude the
athlete is twice as trainable as anyone alive; the priors wash out as the
history grows, and `dataWeight` reports which regime the answer is in.
Parameter covariance is `s²·(JᵀJ + Λ)⁻¹`.

### Uncertainty

The delta method pushes that covariance forward — `Var[V(t)] ≈ ∇θV·Σ·∇θV'`,
plus the residual spread — into the band on every projection and the
probability on every goal, `Φ((V(t) − target)/sd)`. A target with a 4% chance
is told it has a 4% chance.

### Whether a goal is possible at all

Checked in the order the constraints bind, each answered with the number that
decided it:

1. **Past the world record** — the target scores above the best performance on
   record, scored through the same equations.
2. **Past the age-graded record** — above that ceiling discounted to the
   athlete's age (flat to 34, then ~0.7%/year, the WMA shape).
3. **Past any trainable ceiling** — above `C(h)` at the largest week the
   athlete could sustain.
4. **Not by that date** — reachable, but later; the earliest date is quoted.
5. **More hours than you have** — reachable by the date, on hours the athlete
   has said they cannot give; the date on the hours they can is quoted.
6. **Reachable** — with the week it needs, zone by zone, and the odds.

### Fitting it to the athlete

Three parameters — starting VDOT, approach rate `k`, responsiveness `r` — by
penalised nonlinear least squares (Levenberg-Marquardt, central-difference
Jacobian over the ODE solve). The penalty is a Gaussian prior on each parameter
at its literature value, so four noisy months cannot conclude the athlete is
twice as trainable as anyone alive; the priors wash out as the history grows,
and `dataWeight` reports which regime the answer is in.

### The posterior

The point fit above is the starting position for the real inference. A point
estimate with a delta-method normal is right when data are plentiful and the
likelihood is quadratic; for one athlete with a handful of noisy months, tightly
correlated parameters and hard bounds, it quietly reports symmetric intervals for
a skewed answer. So the model is sampled instead — adaptive Metropolis, four
chains, proposals shaped by the Cholesky factor of the chain's own covariance
(the parameters are correlated enough that a diagonal proposal is rejected 98% of
the time), with split-R̂ and effective sample size reported rather than assumed.

A fifth parameter joins the three above: the **scale error in pace-at-heart-rate
as a measure of fitness**. A treadmill that reads fast, a strap that reads high
and the imperfection of the normalisation itself all bias the proxy without
biasing a race, so that bias is estimated instead of assumed away. A synthetic
series read 4% fast is recovered at 0.957.

**What is and is not identified.** The parameters lie on a ridge: a proxy read
low, from a low start, approached slowly fits about as well as an accurate one
from a high start approached fast. The marginals therefore lean on their priors,
and presenting `k` as *measured* would be dishonest. What survives the ridge is
the prediction — every combination on it fits the past and they agree closely
about the future — which is why the predictive intervals are calibrated where the
marginals are not. A time trial cuts across the ridge, because it observes
fitness without the proxy's scale: two of them take a thirty-month prediction
from 3.9 VDOT wide to 1.3.

Sampling takes ~2 s and is cached per version of the history, invalidated by
every import, sync and settings write.

### One solver, five factors

Weekly hours, compliance, race weight, strength hours and the date are all
factors, and each relationship is monotone, so one root-find answers every
direction: name a target, mark one factor as the unknown, and it is solved for —
once per posterior draw, so the answer is a distribution. Draws where nothing in
the bracket reaches the target are reported as having no answer rather than
clamped to the edge, and draws needing no change are counted separately, because
a median over a mixture of "needs nine hours" and "no change needed" means
nothing.

The derivatives come from the same evaluation. Elasticity — the percentage change
in race time per percentage change in a factor — is what makes an hour, a
kilogram and a compliance point comparable, and a two-factor sweep with the
target isoline traced through it answers what happens when both move.

### What to measure next

The model's real limit is how thin the record is, not its mathematics, so it
prices the fix. For a candidate measurement, the value it might return is drawn
from the current posterior predictive, the existing draws are reweighted by how
well each explains that value, and the spread of the prediction under those
weights is the expected result. Reweighting rather than re-sampling is what makes
it cheap enough to offer for every candidate at once.

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
