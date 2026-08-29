/**
 * The shared handles every course chip needs, so they do not have to be threaded
 * through the board, the term list and the pool one prop at a time.
 */
import { createContext, useContext } from "react";
import type { PlannerModel } from "../model/PlannerModel";

export interface PlannerContextValue {
  model: PlannerModel;
  update: (fn: (model: PlannerModel) => void) => void;
  /** Light or dark, read once at the top so chips do not each ask the theme. */
  mode: "light" | "dark";
  /** Course being dragged, or null. */
  drag: string | null;
  setDrag: (code: string | null) => void;
  /** Open the detail card without pinning it. */
  hoverDetail: (code: string, anchor: HTMLElement) => void;
  /** Start the grace period that dismisses an unpinned card. */
  releaseDetail: () => void;
  /** Pin the detail card, or unpin it when the same course is clicked again. */
  pinDetail: (code: string, anchor: HTMLElement) => void;
  /** Courses applied to the degree. */
  applied: Set<string>;
  /** Courses pulled in only because something else requires them. */
  auto: Set<string>;
  /** Courses whose prerequisites are violated where they sit. */
  broken: Set<string>;
  /** Courses the focused course still needs. */
  needed: Set<string>;
}

const PlannerContext = createContext<PlannerContextValue | null>(null);

export const PlannerProvider = PlannerContext.Provider;

export function usePlannerContext(): PlannerContextValue {
  const v = useContext(PlannerContext);
  if (!v)
    throw new Error("usePlannerContext must be used inside a PlannerProvider");
  return v;
}
