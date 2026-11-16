// @vitest-environment jsdom
/**
 * A Lean question on the page: the editor opens on the statement by
 * sorry, a check goes to /api/training/drills/prove, and only a proof the
 * grader reports proved is graded right.
 */
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import type { Answer } from "../../core/assessment";
import type { Question } from "../../core/syllabus";
import QuestionCard from "../QuestionCard";

const question: Question = {
  id: "q-test-lean",
  type: "lean",
  prompt: "In Lean, prove that n = n.",
  why: "Equality is reflexive.",
  ref: { label: "A reference page", url: "https://example.com/ref" },
  statement: "∀ n : Nat, n = n",
};

let check: { status: number; body: unknown };
let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn(async () => respond(check.status, check.body));
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

function show(onAnswer: (answer: Answer) => void = () => {}) {
  render(<QuestionCard question={question} onAnswer={onAnswer} />);
  return screen.getByRole("article", { name: /In Lean, prove that n = n/ });
}

describe("a Lean question", () => {
  it("opens on the statement by sorry, with Submit held until a check", () => {
    const card = show();
    expect(within(card).getByText("Lean proof")).toBeTruthy();
    expect(
      (
        within(card).getByRole("textbox", {
          name: "Your Lean",
        }) as HTMLTextAreaElement
      ).value,
    ).toBe("theorem answer : ∀ n : Nat, n = n := by\n  sorry");
    expect(
      (
        within(card).getByRole("button", {
          name: "Submit",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
  });

  it("sends the file and the statement, and shows why sorry fails", async () => {
    check = {
      status: 200,
      body: {
        outcome: "sorry",
        message: "The proof uses `sorry`.",
        output: "<source>:1:8: warning: declaration uses `sorry`",
      },
    };
    const answers: Answer[] = [];
    const card = show((answer) => answers.push(answer));
    await act(async () => {
      fireEvent.click(
        within(card).getByRole("button", { name: "Check with Lean" }),
      );
    });
    const [url, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/training/drills/prove");
    expect(JSON.parse(String(options.body))).toEqual({
      source: "theorem answer : ∀ n : Nat, n = n := by\n  sorry",
      statement: "∀ n : Nat, n = n",
    });
    const report = within(card).getByRole("alert", { name: "Lean's check" });
    expect(report.textContent).toContain("The proof uses `sorry`.");
    expect(report.textContent).toContain("declaration uses `sorry`");
    expect(
      within(report).getByRole("link", {
        name: "Theorem Proving in Lean 4, Tactics",
      }),
    ).toBeTruthy();
    fireEvent.click(within(card).getByRole("button", { name: "Submit" }));
    expect(answers).toEqual([
      {
        correct: false,
        given: "theorem answer : ∀ n : Nat, n = n := by\n  sorry",
      },
    ]);
  });

  it("grades a proved proof right", async () => {
    check = {
      status: 200,
      body: { outcome: "proved", message: null, output: "" },
    };
    const answers: Answer[] = [];
    const card = show((answer) => answers.push(answer));
    const editor = within(card).getByRole("textbox", { name: "Your Lean" });
    fireEvent.change(editor, {
      target: { value: "theorem answer : ∀ n : Nat, n = n := fun _ => rfl" },
    });
    await act(async () => {
      fireEvent.click(
        within(card).getByRole("button", { name: "Check with Lean" }),
      );
    });
    expect(
      within(card).getByRole("alert", { name: "Lean's check" }).textContent,
    ).toContain("Lean accepted the proof.");
    fireEvent.click(within(card).getByRole("button", { name: "Submit" }));
    expect(answers).toEqual([
      {
        correct: true,
        given: "theorem answer : ∀ n : Nat, n = n := fun _ => rfl",
      },
    ]);
  });

  it("holds Submit when the grader could not check, and says why", async () => {
    check = {
      status: 200,
      body: {
        outcome: "unavailable",
        message: "The grader did not answer.",
        output: "",
      },
    };
    const card = show();
    await act(async () => {
      fireEvent.click(
        within(card).getByRole("button", { name: "Check with Lean" }),
      );
    });
    expect(
      within(card).getByRole("alert", { name: "Lean's check" }).textContent,
    ).toContain("The grader did not answer.");
    expect(
      (
        within(card).getByRole("button", {
          name: "Submit",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
  });

  it("says when the server limits checks", async () => {
    check = { status: 429, body: null };
    const card = show();
    await act(async () => {
      fireEvent.click(
        within(card).getByRole("button", { name: "Check with Lean" }),
      );
    });
    expect(within(card).getByRole("alert").textContent).toContain(
      "Too many checks this minute.",
    );
  });
});
