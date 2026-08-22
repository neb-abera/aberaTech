/** The shapes the scheduling API returns. Instants are always ISO-8601 UTC. */

export type ScheduleMode = 'slots' | 'queue' | 'unavailable';

export interface SlotView {
  startsAt: string;
  endsAt: string;
  minutes: number;
}

export interface QueueView {
  name: string;
  waiting: number;
  nextStartsAt: string | null;
}

export interface ScheduleState {
  mode: ScheduleMode;
  hostName: string;
  viewerZoneId: string;
  slots: SlotView[];
  queue: QueueView | null;
  days: number;
  moreDays: boolean;
}

export interface MyPlace {
  id: string;
  position: number;
  ahead: number;
  state: string;
  projectedStart: string | null;
  minutesAway: number | null;
}

export interface BookingConfirmation {
  id: string;
  startsAt: string;
  endsAt: string;
}
