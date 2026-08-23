import { useEffect, useState } from 'react';

/**
 * Whether the visitor is signed in.
 *
 * The only accounts this site has are the ones that may administer the
 * scheduling queue, so that endpoint is the whole of "is anybody signed in".
 * Wrapped in its own hook rather than called straight from the app bar, so the
 * day a second kind of account exists there is one place to change.
 *
 * Fails closed: any error, and any deployment where the endpoint is not there,
 * answers no. The only thing gated on it is an extra menu entry, so being wrong
 * in that direction costs a preference rather than access to anything.
 */
export function useAccount(): { signedIn: boolean } {
  const [signedIn, setSignedIn] = useState(false);

  useEffect(() => {
    const controller = new AbortController();

    void (async () => {
      try {
        const response = await fetch('/api/scheduling/admin/me', { signal: controller.signal });
        if (!response.ok) return;

        const body = (await response.json()) as { signedIn?: boolean };
        setSignedIn(body.signedIn === true);
      } catch {
        // Offline, aborted, or a deployment that does not serve this route.
        // Either way, not signed in.
      }
    })();

    return () => controller.abort();
  }, []);

  return { signedIn };
}
