import { useEffect, useState } from 'react';

/**
 * Whether the visitor is signed in.
 *
 * The only accounts this site has are the ones that may administer the
 * scheduling queue, so that endpoint is the whole of "is anybody signed in".
 * Wrapped in its own hook rather than called straight from the app bar, so the
 * day a second kind of account exists there is one place to change.
 *
 * The probe is cached at module scope: the app bar mounts its account-aware
 * control twice (desktop and phone variants), and each mount sharing one
 * promise means one request per page load instead of one per mount.
 *
 * Fails closed: any error, and any deployment where the endpoint is not there,
 * answers no — and the answer is cached for the life of the page like a
 * success is. The only thing gated on it is an extra menu entry, so being
 * wrong in that direction costs a preference rather than access to anything.
 */

let probe: Promise<boolean> | null = null;

async function fetchSignedIn(): Promise<boolean> {
  try {
    const response = await fetch('/api/scheduling/admin/me');
    if (!response.ok) return false;

    const body = (await response.json()) as { signedIn?: boolean };
    return body.signedIn === true;
  } catch {
    // Offline, or a deployment that does not serve this route.
    return false;
  }
}

export function useAccount(): { signedIn: boolean } {
  const [signedIn, setSignedIn] = useState(false);

  useEffect(() => {
    let cancelled = false;

    probe ??= fetchSignedIn();
    void probe.then((value) => {
      if (!cancelled) setSignedIn(value);
    });

    return () => {
      cancelled = true;
    };
  }, []);

  return { signedIn };
}

/** Tests share module state through the cache above; let them clear it. */
export function resetAccountProbeForTests(): void {
  probe = null;
}
