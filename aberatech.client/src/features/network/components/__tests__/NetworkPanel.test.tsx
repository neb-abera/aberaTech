// @vitest-environment jsdom
/**
 * The network page from two chairs. A visitor gets a sign-in button that
 * returns here, and the document is never asked for. The owner sees the
 * picture and the list, selects a node for its detail, narrows the picture
 * with a filter, and edits the document as JSON: a bad document is refused
 * with its reasons and nothing is saved, a good one is drawn and saved a
 * beat later.
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
import { resetAccountProbeForTests } from "../../../../hooks/useAccount";
import { respond } from "../../../../test/fakeFetch";
import { example, type NetworkDocument } from "../../core/model";
import NetworkPanel from "../NetworkPanel";

let fetchMock: ReturnType<typeof vi.fn>;

function visit(saved: NetworkDocument | null | "visitor") {
  fetchMock = vi.fn();
  if (saved === "visitor")
    fetchMock.mockResolvedValue(respond(200, { signedIn: false }));
  else {
    fetchMock
      .mockResolvedValueOnce(respond(200, { signedIn: true }))
      .mockResolvedValueOnce(saved ? respond(200, saved) : respond(404))
      .mockResolvedValue(respond(204));
  }
  vi.stubGlobal("fetch", fetchMock);
}

const settle = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
  });
};

const flushSave = async () => {
  await act(async () => {
    vi.advanceTimersByTime(1000);
  });
  await settle();
};

const puts = () =>
  fetchMock.mock.calls.filter(([, options]) => options?.method === "PUT");
const gets = () => fetchMock.mock.calls.map(([url]) => String(url));

beforeEach(() => {
  resetAccountProbeForTests();
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("a visitor", () => {
  it("is offered sign-in that comes back here, and the document is never requested", async () => {
    visit("visitor");
    render(<NetworkPanel />);
    const button = await screen.findByRole("link", {
      name: "Sign in with Google",
    });
    expect(button.getAttribute("href")).toBe(
      "/api/scheduling/admin/sign-in?returnUrl=/network",
    );
    expect(gets()).toEqual(["/api/scheduling/admin/me"]);
    expect(screen.queryByTestId("network-graph")).toBeNull();
  });
});

describe("the owner", () => {
  it("sees the picture and the list, and the detail of a selected node", async () => {
    visit(example());
    render(<NetworkPanel />);
    const graph = await screen.findByTestId("network-graph");
    expect(gets()).toEqual([
      "/api/scheduling/admin/me",
      "/api/progress/network",
    ]);
    expect(graph.getAttribute("aria-label")).toBe(
      "Network map: 2 people and 2 organizations",
    );
    expect(within(graph).getAllByRole("button")).toHaveLength(4);
    expect(
      screen.getByText("2 people, 2 organizations, edited 2026-01-01."),
    ).toBeTruthy();

    const list = screen.getByRole("table", { name: "People" });
    const rows = within(list).getAllByRole("row").slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringContaining("Ada Example"),
      expect.stringContaining("Bo Example"),
    ]);

    fireEvent.click(
      within(graph).getByRole("button", { name: "Ada Example, pending" }),
    );
    const detail = screen.getByRole("complementary", { name: "Selected" });
    expect(
      within(detail).getByRole("heading", { name: "Ada Example" }),
    ).toBeTruthy();
    expect(
      within(detail).getByText("Runs the team that builds what you build."),
    ).toBeTruthy();
    expect(within(detail).getByText("3 mutual")).toBeTruthy();
    expect(
      within(detail)
        .getByRole("link", { name: "Open profile" })
        .getAttribute("href"),
    ).toBe("https://example.com/ada");

    fireEvent.click(
      within(graph).getByRole("button", { name: "Acme Radio, organization" }),
    );
    expect(
      within(detail).getByRole("heading", { name: "Acme Radio" }),
    ).toBeTruthy();
    expect(
      within(detail).getByText("2 people: Ada Example, Bo Example"),
    ).toBeTruthy();
    expect(puts()).toEqual([]);
  });

  it("narrows the picture with a sector and a search, without moving the nodes", async () => {
    visit(example());
    render(<NetworkPanel />);
    const graph = await screen.findByTestId("network-graph");
    const ada = within(graph).getByRole("button", {
      name: "Ada Example, pending",
    });
    const before = ada.getAttribute("transform");

    fireEvent.click(screen.getByRole("button", { name: "Engineering" }));
    expect(ada.getAttribute("data-dimmed")).toBe("true");
    expect(
      within(graph)
        .getByRole("button", { name: "Bo Example, followed" })
        .getAttribute("data-dimmed"),
    ).toBeNull();
    expect(ada.getAttribute("transform")).toBe(before);
    expect(screen.getAllByRole("row")).toHaveLength(2);

    fireEvent.click(screen.getByRole("button", { name: "Engineering" }));
    fireEvent.change(screen.getByLabelText("Find"), {
      target: { value: "partner" },
    });
    expect(ada.getAttribute("data-dimmed")).toBe("true");
    expect(
      within(graph)
        .getByRole("button", { name: "Bo Example, followed" })
        .getAttribute("data-dimmed"),
    ).toBeNull();

    fireEvent.change(screen.getByLabelText("Find"), {
      target: { value: "nobody here" },
    });
    expect(screen.getByText("Nobody matches the filters.")).toBeTruthy();
  });

  it("starts from the example when there is nothing yet, and saves it", async () => {
    visit(null);
    render(<NetworkPanel />);
    await screen.findByText(
      "No network yet. Start from the example, upload a document, or open the editor.",
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Start from the example" }),
    );
    expect(screen.getByTestId("network-graph")).toBeTruthy();
    await flushSave();
    const [[url, options]] = puts();
    expect(url).toBe("/api/progress/network");
    expect(JSON.parse(String(options.body))).toEqual(example());
  });

  it("refuses a document with problems and saves nothing", async () => {
    visit(example());
    render(<NetworkPanel />);
    await screen.findByTestId("network-graph");
    fireEvent.click(screen.getByRole("button", { name: "Edit data" }));
    const editor = screen.getByLabelText("Document JSON");
    expect((editor as HTMLTextAreaElement).value).toContain('"Ada Example"');
    fireEvent.change(editor, {
      target: {
        value:
          '{"sectors":[],"organizations":[],"people":[{"id":"q","name":"Q","sector":"s","tier":9,"status":"x"}]}',
      },
    });
    fireEvent.click(screen.getByRole("button", { name: "Apply" }));
    const alert = screen.getByRole("alert");
    expect(alert.textContent).toContain("Not applied. 3 problems:");
    expect(alert.textContent).toContain(
      'Person "q" names a sector that is not listed',
    );
    await flushSave();
    expect(puts()).toEqual([]);
    expect(screen.getByTestId("network-graph").getAttribute("aria-label")).toBe(
      "Network map: 2 people and 2 organizations",
    );
  });

  it("applies a good document, draws it and saves it a beat later", async () => {
    visit(example());
    render(<NetworkPanel />);
    await screen.findByTestId("network-graph");
    fireEvent.click(screen.getByRole("button", { name: "Edit data" }));
    const next: NetworkDocument = {
      ...example(),
      updated: "2026-10-02",
      people: [
        ...example().people,
        {
          id: "cy",
          name: "Cy Example",
          role: "Founder",
          sector: "capital",
          orgs: [],
          tier: 2,
          status: "connected",
        },
      ],
    };
    fireEvent.change(screen.getByLabelText("Document JSON"), {
      target: { value: JSON.stringify(next) },
    });
    fireEvent.click(screen.getByRole("button", { name: "Apply" }));
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.queryByLabelText("Document JSON")).toBeNull();
    const graph = screen.getByTestId("network-graph");
    expect(graph.getAttribute("aria-label")).toBe(
      "Network map: 3 people and 2 organizations",
    );
    expect(
      within(graph).getByRole("button", { name: "Cy Example, connected" }),
    ).toBeTruthy();
    expect(screen.getByText("Saving…")).toBeTruthy();
    await flushSave();
    const [[url, options]] = puts();
    expect(url).toBe("/api/progress/network");
    expect(JSON.parse(String(options.body))).toEqual(next);
    expect(screen.getByText("Saved")).toBeTruthy();
  });
});
