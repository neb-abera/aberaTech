# /training

A course player for competitive math and programming. It coordinates other
systems and writes no lesson text a source already teaches. The design is
the Notion page "abera.tech training system for competitive math and
programming: systems design (9 Nov 2026)". The plan it delivers is
"Competitive math and programming: plan to 2032".

## Where things are

- `data/syllabus.json`: phases, the week, systems, units, lessons, items,
  and checkpoints. Built on 9 Nov 2026 from the plan and from each source.
- `data/link-check.json`: how every address in the syllabus was opened, and
  what it answered. A unit test fails on an address with no record.
- `core/syllabus.ts`: the types, including the question types the
  assessment increment fills.
- `core/progress.ts`: the owner's document (key `training` in
  `/api/progress`), item status, checkpoints, and unit states.
- `/api/progress/training`: owner only, server side. A visitor never asks
  for it.

## Self-paced

Next up is the first screen for the owner. It lists the next unfinished
work in syllabus order across both tracks, interleaved by the plan's split
of self-paced minutes between math and programming, and filled to one
session. The session's default is the plan's self-paced minutes a week over
its sessions: 60 in Phases 0 and 1. Keep going adds the next item, without
limit. No date opens or holds back a unit. A phase's dates are its targets.

Pace shows this week's minutes against the plan's, the last 28 days as
minutes a week, and the day each phase's written work would be done at
that pace. Contests run at set times, so they sit in a Scheduled panel with
their start times from the Codeforces API and AtCoder's contest list. They
never hold back or count toward a unit.

## How the syllabus was built

| Data                              | Source                                                                 |
| --------------------------------- | ---------------------------------------------------------------------- |
| Chapters, sections, pages         | The AoPS table of contents PDF for each book                           |
| Videos                            | AoPS's videos page, read in Chrome, and each video's length on YouTube |
| USACO Guide modules               | `content/ordering.ts` and the module files in cpinitiative/usaco-guide |
| CSES problems                     | cses.fi/problemset                                                     |
| Codeforces problems and ratings   | The Codeforces API, `problemset.problems`                              |
| AtCoder problems and difficulties | AtCoder Problems, `problems.json` and `problem-models.json`            |

Minutes for reading are 3 a page, an estimate, and each item says so.

## Increments not built yet

Each is in the design page, with its requirement.

1. Review queue at 2 and 14 days, and the rule that overdue reviews come
   first. The document keeps `reviews`, and `today()` takes a list of due
   reviews and puts them first in their track's block.
2. Assessment: multiple choice, short answer, proofs against a rubric,
   flashcards on FSRS, an Anki export, pretests, and mastery checks. Lessons
   take `questions`, and units take `pretest` and `mastery`.
3. Notes search. Notes are kept by lesson and item id in `notes`.
4. A Rust grader service beside the site, called by the server, for drills
   beyond the basics. Not built until Neb approves its cost.

## Drills

Five basics drills in "C++ for contests: the language" are coded on the
page. `/api/training/drills/run` sends the source and each test's input to
Compiler Explorer's public API (gcc 16.2, `-O2 -std=c++23`), and compares
the output with the test, ignoring trailing spaces and blank lines. No
code runs in the site's container. Each drill's expected output came from a
reference solution compiled and run on the dev box on 9 Nov 2026.
