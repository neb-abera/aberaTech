import { describe, expect, it } from "vitest";
import { linkHeaders, linkUrl, readLinkKey } from "../link";

function memoryStore(initial: Record<string, string> = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => void values.set(key, value),
    values,
  };
}

const key = "Q2hhbmdlTWVQbGVhc2UxMg";

describe("readLinkKey", () => {
  it("reads the key after #k= and keeps it for the tab", () => {
    const store = memoryStore();

    expect(readLinkKey(`#k=${key}`, store)).toBe(key);
    expect(readLinkKey("", store)).toBe(key);
  });

  it("lets a new link replace the kept one", () => {
    const store = memoryStore();
    readLinkKey(`#k=${key}`, store);

    expect(readLinkKey("#k=another_key-1", store)).toBe("another_key-1");
    expect(readLinkKey("", store)).toBe("another_key-1");
  });

  it("is null with no link", () => {
    expect(readLinkKey("", memoryStore())).toBeNull();
    expect(readLinkKey("#section", memoryStore())).toBeNull();
    expect(readLinkKey(`#k=${key}`, undefined)).toBe(key);
    expect(readLinkKey("", undefined)).toBeNull();
  });

  it("ignores anything that cannot be a key", () => {
    const store = memoryStore();

    expect(readLinkKey("#k=<script>", store)).toBeNull();
    expect(readLinkKey(`#k=${"a".repeat(65)}`, store)).toBeNull();
    expect(store.values.size).toBe(0);
    expect(
      readLinkKey("", memoryStore({ "aberatech.scheduling.link": "bad key" })),
    ).toBeNull();
  });
});

describe("linkHeaders and linkUrl", () => {
  it("sends the key in one header, or nothing", () => {
    expect(linkHeaders(key)).toEqual({ "X-Booking-Link": key });
    expect(linkHeaders(null)).toEqual({});
  });

  it("puts the key after #, where the browser keeps it to itself", () => {
    expect(linkUrl("https://abera.tech", key)).toBe(
      `https://abera.tech/schedule#k=${key}`,
    );
  });
});
