import { FETCH_TIMEOUT_MS } from '@/constants/timeouts';

/**
 * Executes an async query operation with a combined caller AbortSignal and hard timeout.
 */
export async function withTimeoutSignal<T>(
  fn: (signal: AbortSignal) => Promise<T>,
  outerSignal?: AbortSignal,
  timeoutMs: number = FETCH_TIMEOUT_MS,
): Promise<T> {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);
  const onAbort = () => controller.abort();
  outerSignal?.addEventListener('abort', onAbort, { once: true });

  try {
    return await fn(controller.signal);
  } finally {
    clearTimeout(timeoutId);
    outerSignal?.removeEventListener('abort', onAbort);
  }
}
