/**
 * The race log's and the place log's calls. Kept out of api.ts, which the
 * site's entry chunk carries, so they load with the Log tab that uses them.
 */
import { ApiError } from "./api";

async function send(url: string, method: string, body?: unknown) {
  const response = await fetch(url, {
    method,
    headers:
      body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }
}

export const enterRace = (race: {
  date: string;
  name: string;
  distanceMeters: number;
  seconds: number;
  altitudeMeters: number | null;
}) => send("/api/fitness/races", "POST", race);

export const removeRace = (id: string) =>
  send(`/api/fitness/races/${encodeURIComponent(id)}`, "DELETE");

export const addPlace = (place: {
  since: string | null;
  place: string;
  altitudeMeters: number;
}) => send("/api/fitness/places", "POST", place);

export const removePlace = (id: string) =>
  send(`/api/fitness/places/${encodeURIComponent(id)}`, "DELETE");
