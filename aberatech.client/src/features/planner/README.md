# Course planner

A planner for the Johns Hopkins Engineering for Professionals master's in
Electrical and Computer Engineering. It holds the whole catalog, enforces the
prerequisites, checks the degree rules, and counts the five year clock from the
first course you **apply** to the degree rather than the first course you take.

Route: `/planner`. Lazy loaded from `src/App.tsx`, so none of this reaches the
main bundle unless the tab is opened.

## Layout

```
core/       pure TypeScript, no React, no DOM, no browser globals
model/      planner state and everything derived from it, still framework free
hooks/      the one seam between the model and React
components/ MUI presentation, no rules of its own
data/       the parsed catalog and the curated tracks, as JSON
```

The rule that keeps this maintainable: **anything that can be decided without a
screen lives in `core/` or `model/`, and is tested there.** A component may
read the model and call a method on it. A component may not decide whether a
course is legal in a term.

## The core

| Module | What it owns |
|---|---|
| `types.ts` | The shapes. A course's `groups` is an AND of ORs: `[['A','B'],['C']]` reads "(A or B) and C". |
| `prereq.ts` | Satisfaction, transitive closure, earliest and latest legal term. |
| `plan.ts` | The immutable `Plan` aggregate. Every operation returns `{ok, plan}` and a refused one returns the same object. |
| `calendar.ts` | Term index to label and real date. |
| `rules.ts` | The four degree rules and the five year clock. |
| `select.ts` | Which ten courses to apply. |
| `background.ts` | Prose prerequisites turned into schedulable preparation courses. |
| `tracks.ts` | The curated tracks and their ordered scheduling. |
| `catalog.ts`, `links.ts`, `areaColors.ts`, `format.ts` | Lookup, outbound links, colour, formatting. |

Two decisions worth knowing before changing anything:

- **A refusal has to say which refusal it is.** `PlannerModel.placementNote`
  separates "the prerequisite is missing" (the planner can insert it and the
  drop is accepted) from "the prerequisite is in the plan but sits later" and
  "moving it here would strand a dependent" (neither can be fixed by dropping,
  so the term refuses and the chip menu says why). A test asserts the
  invariant: every term the interface offers actually accepts the course.
- **Capacity is a preference, not a rule.** A term over the courses per term
  setting is flagged and allowed. Treating it as a violation once made the board
  immovable as soon as it filled up.
- **The five year clock spans the applied ten, not the whole plan.** You can
  take a hundred courses and still graduate on time; you cannot apply a course
  that sits ten years out. `select.ts` exists entirely because of this: taking
  the first ten in term order can never satisfy "at least four at the 700 level",
  since prerequisite order puts the foundation courses first.

## The model

`PlannerModel` is mutable on purpose. A single operation touches the plan, the
selection and the derived catalog together, and the rules read across all of
them, so mirroring it into React state would give two copies that drift.
`PlannerStore` wraps it and publishes a version number; `usePlanner` subscribes
through `useSyncExternalStore`. Mutations go through `update(m => ...)`, which
bumps the version and re-renders.

## Tests

```
npm test          # once
npm run test:watch
```

Vitest, 207 assertions, no DOM required. They cover the catalog itself
(`catalog.regression.test.ts` encodes ten prerequisite readings that were parsed
wrongly at some point and must never regress), every curated track scheduled by
the real engine and audited against the real rules, and the model against the
real 138 course catalog.

## What the components may not do

They may not decide legality, ordering, or what counts toward the degree. If a
component needs to know whether something is allowed, the answer belongs on
`PlannerModel` where a test can reach it. That is the whole reason the interface
can be rewritten without re-verifying the rules.

## Data

`data/catalog.json` is generated from the JHU e-catalogue PDF, not hand written.
Regenerating it is a separate offline step; the regression test is what keeps a
bad regeneration from shipping.
