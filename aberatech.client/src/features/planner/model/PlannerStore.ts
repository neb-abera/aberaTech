/**
 * A tiny observable wrapper around PlannerModel.
 *
 * The model is mutable by design: a plan operation touches several fields at
 * once and the rules read across all of them. Rather than mirror that into
 * React state and risk the two drifting, the model stays the single source of
 * truth and the store publishes a version number that React subscribes to.
 */
import type { PlannerModel } from "./PlannerModel";

export class PlannerStore {
  readonly model: PlannerModel;
  private listeners = new Set<() => void>();
  private version = 0;

  constructor(model: PlannerModel) {
    this.model = model;
  }

  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  /** The snapshot is a number on purpose: it is cheap and always compares by value. */
  getVersion = (): number => this.version;

  /** Run a mutation, then tell every subscriber the model moved. */
  update = (fn: (model: PlannerModel) => void): void => {
    fn(this.model);
    this.version += 1;
    this.listeners.forEach((l) => {
      l();
    });
  };
}
