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
  closesAt: string;
  estimatedStartIfYouJoin: string | null;
  acceptingJoins: boolean;
}

export interface ScheduleState {
  mode: ScheduleMode;
  hostName: string;
  viewerZoneId: string;
  slots: SlotView[];
  queue: QueueView | null;
  availableDates: string[] | null;
  selectedDate: string | null;
}

export interface MyPlace {
  id: string;
  position: number;
  ahead: number;
  state: string;
  projectedStart: string | null;
  minutesAway: number | null;
  beyondClose: boolean;
}

export interface BookingConfirmation {
  id: string;
  startsAt: string;
  endsAt: string;
}
