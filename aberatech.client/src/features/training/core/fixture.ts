/**
 * Sample questions the server sends under `make e2e` only
 * (FakeAssessment.cs, Development with Training:Fake). The syllabus holds
 * no questions until its content is written, so the owner suite adds these
 * to tell a failed check from a passed one. Production sends none, and this
 * changes nothing there.
 */
import type { ExternalCheck, Question, Syllabus } from "./syllabus";
import { syllabus } from "./syllabus";

export interface Fixture {
  lessons?: Record<string, Question[]>;
  units?: Record<
    string,
    {
      pretest?: string[];
      mastery?: string[];
      placement?: ExternalCheck;
      externalMastery?: ExternalCheck;
    }
  >;
}

const applied = new WeakSet<object>();

/** Add the fixture's questions and checks, once. */
export function applyFixture(
  fixture: Fixture | null | undefined,
  into: Syllabus = syllabus,
): boolean {
  if (!fixture || applied.has(fixture)) return false;
  applied.add(fixture);
  for (const unit of into.units) {
    for (const lesson of unit.lessons) {
      const add = fixture.lessons?.[lesson.id];
      if (!add) continue;
      const ids = new Set(add.map((question) => question.id));
      lesson.questions = [
        ...(lesson.questions ?? []).filter((question) => !ids.has(question.id)),
        ...add,
      ];
    }
    const checks = fixture.units?.[unit.id];
    if (checks) Object.assign(unit, checks);
  }
  return true;
}
