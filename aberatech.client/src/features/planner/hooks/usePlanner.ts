/** Wiring the planner store into React, and the owner's saved plan into the store. */
import { useEffect, useMemo, useRef, useSyncExternalStore } from "react";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import { CatalogData } from "../core/catalog";
import { Tracks } from "../core/tracks";
import type { RawCatalog, RawTracks } from "../core/types";
import rawCatalog from "../data/catalog.json";
import rawTracks from "../data/tracks.json";
import {
  DEFAULT_TRACK,
  PlannerModel,
  type PlannerSnapshot,
} from "../model/PlannerModel";
import { PlannerStore } from "../model/PlannerStore";

export interface Planner {
  store: PlannerStore;
  model: PlannerModel;
  update: (fn: (model: PlannerModel) => void) => void;
  /** Bumped on every mutation. Depend on it to recompute derived values. */
  version: number;
  /** Whether this plan is being kept: the owner's is, a visitor's is not. */
  saved: "loading" | "visitor" | "owner" | "error";
  /** A save is scheduled or in flight. */
  saving: boolean;
}

export const documentKey = "planner";

function createStore(): PlannerStore {
  const data = new CatalogData(rawCatalog as unknown as RawCatalog);
  const tracks = new Tracks(rawTracks as unknown as RawTracks);
  const model = new PlannerModel(data, tracks);
  // Open on a curated track rather than a bare focus area dump.
  model.selectTrack(DEFAULT_TRACK);
  return new PlannerStore(model);
}

/**
 * The owner's plan is loaded once the server says who is asking, restored
 * into the store, and then every mutation is saved a beat later. A visitor
 * gets the same board with nothing behind it: their changes live until the
 * page closes and are never sent.
 */
export function usePlanner(): Planner {
  const store = useMemo(createStore, []);
  const version = useSyncExternalStore(
    store.subscribe,
    store.getVersion,
    store.getVersion,
  );
  const { status, value, set, saving } =
    useOwnerDocument<PlannerSnapshot>(documentKey);

  // Restore exactly once, when the document first arrives; the version that
  // restore bumps must not itself be saved back as a change.
  const restored = useRef(false);
  const restoredVersion = useRef<number | null>(null);
  useEffect(() => {
    if (status !== "owner" || restored.current) return;
    restored.current = true;
    if (value) {
      store.update((model) => {
        model.restore(value);
      });
    }
    restoredVersion.current = store.getVersion();
  }, [status, value, store]);

  // version is the mutation counter; each bump is a change to save.
  useEffect(() => {
    if (status !== "owner" || !restored.current) return;
    if (version === restoredVersion.current) return;
    set(store.model.snapshot());
  }, [version, status, set, store]);

  return {
    store,
    model: store.model,
    update: store.update,
    version,
    saved: status,
    saving,
  };
}
