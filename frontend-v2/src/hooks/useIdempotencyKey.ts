'use client';

import { useCallback, useRef } from 'react';

/**
 * Coordinates a stable Idempotency-Key across retry attempts of a user mutation,
 * rotating when a new dialog/flow starts and resetting after success.
 */
export function useIdempotencyKey() {
  const keyRef = useRef<string | null>(null);

  const getKey = useCallback((): string => {
    if (!keyRef.current) {
      keyRef.current = crypto.randomUUID();
    }
    return keyRef.current;
  }, []);

  const rotateKey = useCallback((): string => {
    const next = crypto.randomUUID();
    keyRef.current = next;
    return next;
  }, []);

  const resetKey = useCallback((): void => {
    keyRef.current = null;
  }, []);

  return { getKey, rotateKey, resetKey };
}
