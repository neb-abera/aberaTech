// @vitest-environment jsdom
/**
 * The plan from the reader's chair: every block on the page, every task a
 * checkbox, and a tick that survives leaving and coming back. The page is
 * the whole feature, so it is tested from the page.
 */

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { allTasks, plan } from "../../core/plan";
import { storageKey } from "../../hooks/useProgress";
import TrainingPlan from "../TrainingPlan";

afterEach(cleanup);

/**
 * An in-memory Storage, provided explicitly, for the same reason
 * ColorModeIconDropdown.race.test.tsx brings its own: Node 26 predefines a
 * global localStorage that is undefined without --localstorage-file, and the
 * jsdom environment defers to that existing global.
 */
function memoryStorage(): Storage {
  const store = new Map<string, string>();
  return {
    get length() {
      return store.size;
    },
    clear: () => store.clear(),
    getItem: (key: string) => store.get(key) ?? null,
    key: (index: number) => [...store.keys()][index] ?? null,
    removeItem: (key: string) => {
      store.delete(key);
    },
    setItem: (key: string, value: string) => {
      store.set(key, String(value));
    },
  };
}

let storage: Storage;

beforeEach(() => {
  storage = memoryStorage();
  vi.stubGlobal("localStorage", storage);
  Object.defineProperty(window, "localStorage", {
    value: storage,
    configurable: true,
  });
});

describe("the training plan", () => {
  it("shows every block with its weeks and its gate", () => {
    render(<TrainingPlan />);

    plan.forEach((block, index) => {
      // Numbered on the page, so the reader can say "block three".
      expect(
        screen.getByRole("heading", { name: `${index + 1}. ${block.title}` }),
      ).toBeTruthy();
      expect(screen.getByText(block.weeks)).toBeTruthy();
      expect(screen.getByText(block.gate)).toBeTruthy();
    });
  });

  it("offers every task as a checkbox, none ticked on first visit", () => {
    render(<TrainingPlan />);

    const boxes = screen.getAllByRole("checkbox");
    expect(boxes).toHaveLength(allTasks.length);
    for (const box of boxes) {
      expect((box as HTMLInputElement).checked).toBe(false);
    }
  });

  it("counts progress as tasks are ticked", () => {
    render(<TrainingPlan />);

    const first = allTasks[0];
    expect(screen.getByText(`0 of ${allTasks.length} done`)).toBeTruthy();

    fireEvent.click(screen.getByRole("checkbox", { name: first.text }));

    expect(screen.getByText(`1 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("keeps a tick across a reload", () => {
    const { unmount } = render(<TrainingPlan />);
    const task = allTasks[3];

    fireEvent.click(screen.getByRole("checkbox", { name: task.text }));
    unmount();
    cleanup();

    render(<TrainingPlan />);

    const box = screen.getByRole("checkbox", {
      name: task.text,
    }) as HTMLInputElement;
    expect(box.checked).toBe(true);
    expect(JSON.parse(storage.getItem(storageKey) ?? "[]")).toEqual([task.id]);
  });

  it("can start over", () => {
    render(<TrainingPlan />);

    fireEvent.click(screen.getByRole("checkbox", { name: allTasks[0].text }));
    fireEvent.click(screen.getByRole("checkbox", { name: allTasks[1].text }));
    fireEvent.click(screen.getByRole("button", { name: "Start over" }));

    expect(screen.getByText(`0 of ${allTasks.length} done`)).toBeTruthy();
    expect(storage.getItem(storageKey)).toBe("[]");
  });

  it("ignores stored progress it cannot read", () => {
    storage.setItem(storageKey, "not json");

    render(<TrainingPlan />);

    expect(screen.getByText(`0 of ${allTasks.length} done`)).toBeTruthy();
  });

  it("opens every resource on another tab, saying so", () => {
    render(<TrainingPlan />);

    for (const block of plan) {
      for (const resource of block.resources) {
        const link = screen.getByRole("link", { name: resource.title });
        expect(link.getAttribute("href")).toBe(resource.url);
        expect(link.getAttribute("target")).toBe("_blank");
        expect(link.getAttribute("rel")).toContain("noopener");
      }
    }
  });
});
