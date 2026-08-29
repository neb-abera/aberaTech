/** Wiring the planner store into React. */
import { useMemo, useSyncExternalStore } from "react";
import { CatalogData } from "../core/catalog";
import { Tracks } from "../core/tracks";
import type { RawCatalog, RawTracks } from "../core/types";
import rawCatalog from "../data/catalog.json";
import rawTracks from "../data/tracks.json";
import { DEFAULT_TRACK, PlannerModel } from "../model/PlannerModel";
import { PlannerStore } from "../model/PlannerStore";

export interface Planner {
  store: PlannerStore;
  model: PlannerModel;
  update: (fn: (model: PlannerModel) => void) => void;
  /** Bumped on every mutation. Depend on it to recompute derived values. */
  version: number;
}

function createStore(): PlannerStore {
  const data = new CatalogData(rawCatalog as unknown as RawCatalog);
  const tracks = new Tracks(rawTracks as unknown as RawTracks);
  const model = new PlannerModel(data, tracks);
  // Open on a curated track rather than a bare focus area dump.
  model.selectTrack(DEFAULT_TRACK);
  return new PlannerStore(model);
}

export function usePlanner(): Planner {
  const store = useMemo(createStore, []);
  const version = useSyncExternalStore(
    store.subscribe,
    store.getVersion,
    store.getVersion,
  );
  return { store, model: store.model, update: store.update, version };
}
