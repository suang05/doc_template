/**
 * Global timeout and delay constants — SSoT for all magic numbers.
 * Import these instead of hardcoding millisecond values in hooks or components.
 */

/** Abort a fetch request after this duration (ms). */
export const FETCH_TIMEOUT_MS = 10_000;

/** Auto-dismiss a success/info toast after this duration (ms). */
export const TOAST_AUTO_DISMISS_MS = 3_000;

/** Delay before revoking a temporary Blob URL (ms). */
export const BLOB_URL_REVOKE_DELAY_MS = 10_000;
