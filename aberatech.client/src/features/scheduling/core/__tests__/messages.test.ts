import { describe, expect, it } from "vitest";
import { kindLabel, messageTitle, stateChip } from "../messages";

describe("kindLabel", () => {
  it("names each message the way the host thinks of it, not the enum", () => {
    expect(kindLabel("Booked")).toBe("Booking confirmation");
    expect(kindLabel("ReminderDayBefore")).toBe("Day-before reminder");
    expect(kindLabel("Reminder")).toBe("Final reminder");
    expect(kindLabel("Cancelled")).toBe("Cancellation");
  });

  it("passes an unknown kind through rather than hiding it", () => {
    // A new server-side kind must show up as itself, not as a blank row that
    // makes the list look shorter than the outbox is.
    expect(kindLabel("SomethingNew")).toBe("SomethingNew");
  });
});

describe("stateChip", () => {
  it("says what each state means for delivery", () => {
    expect(stateChip("Pending").label).toBe("Queued");
    expect(stateChip("Failed").label).toBe("Retrying");
    expect(stateChip("Sent").label).toBe("Sent, awaiting receipt");
    expect(stateChip("Delivered").label).toBe("Delivered");
    expect(stateChip("DeadLettered").label).toBe("Not delivered");
  });

  it("colors the terminal failure as the emergency it is", () => {
    // Dead letters are the case the outbox exists to make loud. Everything
    // else can be calm.
    expect(stateChip("DeadLettered").color).toBe("error");
    expect(stateChip("Delivered").color).toBe("success");
    expect(stateChip("Failed").color).toBe("warning");
  });

  it("shows an unknown state as itself", () => {
    expect(stateChip("Mystery")).toEqual({
      label: "Mystery",
      color: "default",
    });
  });
});

describe("calendar jobs", () => {
  it("names the address from the body and claims only what Google said", () => {
    const invite = {
      kind: "CalendarInvite",
      to: "",
      body: "Calendar invite to ada@example.com",
    };

    expect(messageTitle(invite)).toBe("Calendar invite to ada@example.com");
    expect(stateChip("Delivered", "CalendarInvite").label).toBe(
      "Google accepted",
    );
    expect(stateChip("Delivered", "Booked").label).toBe("Delivered");
    expect(stateChip("DeadLettered", "CalendarInvite").label).toBe(
      "Not delivered",
    );
    expect(kindLabel("HostInviteFailed")).toBe("Invite failed, to you");
  });

  it("keeps the kind and number for a text", () => {
    expect(
      messageTitle({ kind: "Booked", to: "+12025550143", body: "x" }),
    ).toBe("Booking confirmation · +12025550143");
  });
});
