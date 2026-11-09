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
- `core/syllabus.ts`: the types, including the questions and checks
  (schema below).
- `core/progress.ts`: the owner's document (key `training` in
  `/api/progress`), item status, checkpoints, and unit states.
- `/api/progress/training`: owner only, server side. A visitor never asks
  for it.
- `/api/training/pool`: Mostafa Saad's pool, owner only, read live.

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
| Educational DP Contest tasks      | atcoder.jp/contests/dp/tasks                                           |
| Zeitz, third edition              | Wiley's table of contents                                              |
| learncpp.com lessons              | learncpp.com's table of contents, by lesson number                     |
| Saad's Udemy sections and lengths | Each course's public curriculum on Udemy, read in Chrome               |
| cppreference pages                | Internet Archive snapshots. Cloudflare holds Chrome at a human check.  |
| Alcumus topics                    | Neb's Alcumus report, matched to each book's section titles            |
| Math placement                    | AoPS's "Do you need this?" test for each class, from its course page   |
| AMC 10 and AMC 12 papers          | AoPS's Practice Competitions page                                      |
| Olympiad book chapters and pages  | Each book's own contents: its PDF, Wiley, MAA, Springer, or AMS        |
| USAMO and IMO papers              | Evan Chen's solution notes, one PDF a year                             |
| IMO Shortlist and medal cutoffs   | imo-official.org                                                       |
| Div. 1 problems under 100 solvers | Official standings of the 40 newest Div. 1 rounds, Codeforces API      |
| ARC and AGC problems              | AtCoder Problems, `problems.json` and `problem-models.json`            |
| cp-algorithms pages               | `src/navigation.md` in cp-algorithms/cp-algorithms                     |

Minutes for reading are 3 a page, an estimate, and each item says so.

## Review

What comes back, and when, is `core/reviews.ts`. A problem marked missed
and a lesson once every item in it is done each enter review under a key,
`item:<id>` or `lesson:<id>`. FSRS schedules each key, through ts-fsrs (MIT)
with its default parameters, 90% target retention, no fuzz, and whole days.
The event sets the first date: a miss rates Again (back the next day), a
finished lesson rates Good (back in 3 days). Each answer after that keeps the
key's card in `cards`, and the key stays in review at growing intervals.

A miss starts successive relearning. The key is due each day until it is
answered right on two later days. Only then does a missed problem count again,
and its missed mark is cleared.

Due reviews come first in Next up, dealt round robin across units. While
more than one session of review is overdue, Next up lists reviews alone and
new material waits. Due today is due. Due before today is overdue.

Notes are searchable under the Notes tab: every word of the query must be in
the note or its title, with case and accents ignored.

## Assessment

`core/assessment.ts` reads the results. `core/grade.ts` grades short answers
by value.

- A lesson with questions ends in a quiz. The lesson is done once every item
  is done and the quiz has been taken, right or wrong.
- A unit's pretest comes first in Next up. 90% or more places the unit out.
  An outside placement test, scored by hand at or above its pass mark, does
  the same.
- A unit's mastery check comes once its lessons are done. 90% or more closes
  the unit. Below that, the check opens again 2 days after the attempt. An
  outside mastery check, ticked by hand with its date, closes the unit the
  same way, and the unit's lessons then enter review.
- Every wrong answer shows the right answer, why, and a link to the
  question's reference. It then comes back in review, in relearning. Every
  card comes back in review on FSRS.
- Tools has the cards as a file Anki imports.

### Schema for questions

Extend `data/syllabus.json` with these fields. Each is optional, and the
types are in `core/syllabus.ts`.

A lesson takes `questions`, a list. Every question has `id` (unique across
the syllabus), `type`, `prompt`, `why` (shown after any answer), and `ref`,
the exact place that teaches it: `{ "label", "url", "at" }`, where `at` is
seconds into a video. A YouTube link opens at that second.

| `type`   | More fields                                       | Graded by                                                            |
| -------- | ------------------------------------------------- | -------------------------------------------------------------------- |
| `choice` | `choices` (strings), `answer` (index)             | The page                                                             |
| `number` | `answer`: `"42"`, `"3/4"`, `"2^10"`, `"n(n+1)/2"` | The page, by value                                                   |
| `proof`  | `rubric`: the points a correct proof makes        | The owner, each line met or not, with elegance and clarity beside it |
| `card`   | `back`                                            | The owner's recall, Again to Easy                                    |
| `code`   | `tests`: `{ "stdin", "stdout" }`, `starter`       | The drill endpoint, every test passing                               |

A short answer may use numbers, `+ - * / ^`, parentheses, `sqrt`, `abs`,
`sin`, `cos`, `tan`, `ln`, `log`, `exp`, `pi`, and single-letter variables.

A unit takes:

- `pretest`: question ids from its lessons, asked before the unit. They are
  kept out of the lesson quizzes.
- `mastery`: question ids from its lessons, asked to close the unit. They
  are kept out of the lesson quizzes too.
- `placement`: an outside placement test, `{ "label", "url", "what",
  "minutes", "pass", "outOf" }`, such as an AoPS course diagnostic.
- `externalMastery`: an outside mastery check, `{ "label", "url", "what",
  "minutes", "topics" }`, such as the named Alcumus topics. The page never
  asks for AoPS credentials.

The syllabus holds 991 questions for Phases 0 to 2: 459 cards, 285 number
answers, 141 choices, 65 proofs, and 41 code questions. 33 programming and
non-Alcumus math units have a pretest and a mastery check. The 26 units
Alcumus covers have cards and proofs only, close on their named Alcumus
topics (`externalMastery`), and place out by AoPS's own test (their
checkpoint). Practice bands, contests, and timed papers have none: the
judges and the paper's score test them. Every number answer was computed by
a script, and every code question's reference solution passed its tests in
gcc 16.2 on the dev box.

Under `make e2e` the server sends two sample questions
(`aberaTech.Server/Training/FakeAssessment.cs`), and `core/fixture.ts` adds
them to the C++ refresher. Production sends none.

## Problem pool and solve log

Next up lists ten problems a week from Mostafa Saad's supervision pool. The
server reads his public sheet (the General list, sorted by level) as CSV at
`/api/training/pool`, owner only, and holds it in memory for at most an hour.
Nothing is stored. It keeps importance P5 to P3. `core/saad.ts` picks by his
mix around your level: 1 below, 2 at, 6 slightly above, and 1 far above.
Solved problems are left out, by the Codeforces API and by the log.

Your level is the open band unit's rating (`band-1000` is 1000), else your
Codeforces rating, else 800. A rating maps to his scale by his table (Div. 2
A 1 to 2, B 1.5 to 3, C 3 to 5.5, D 5 to 6.5, E 6 to 7.5) at the median
Codeforces rating of each Div. 2 letter, read from the Codeforces API on 9 Nov
2026: A 800, B 1200, C 1600, D 1900, E 2300.

The Log tab keeps his columns per solve: reading, thinking, coding, and
debugging minutes, submissions, and alone, with a hint, or after the
editorial. It shows weekly averages, flags reading over 5 minutes and coding
or debugging over 10 (his marks), times speed drills against his targets
(Div. 2 A in 5 minutes, B in 10), and holds his weekly checklist from the
Junior Training Sheet.

## Drills

Five basics drills in "C++ for contests: the language" are coded on the
page. `/api/training/drills/run` sends the source and each test's input to
Compiler Explorer's public API (gcc 16.2, `-O2 -std=c++23`), and compares
the output with the test, ignoring trailing spaces and blank lines. No
code runs in the site's container. Each drill's expected output came from a
reference solution compiled and run on the dev box on 9 Nov 2026.

The refresher at the start of Phase 0 adds twelve drills, one or more for
each C++ and STL topic. Each drill's expected output came from a Python
model of the task, and a C++ reference solution compiled with gcc 16.2,
`-O2 -std=c++23`, in a container on the dev box matched it on every test.

With `Training:GraderUrl` set, the server sends the drill to the Rust
grader (`grader/README.md`), which calls Compiler Explorer and returns a
verdict per test. Without it, the server calls Compiler Explorer itself.
