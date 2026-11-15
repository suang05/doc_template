import { z } from 'zod';

export const problemDetailsErrorItemSchema = z.object({
  pointer: z.string(),
  code: z.string(),
  message: z.string(),
});

export type ProblemDetailsErrorItem = z.infer<typeof problemDetailsErrorItemSchema>;

export const problemDetailsSchema = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().int().optional(),
  detail: z.string().optional(),
  instance: z.string().optional(),
  errorCode: z.string().optional(),
  traceId: z.string().optional(),
  errors: z.array(problemDetailsErrorItemSchema).optional(),
});

export type ProblemDetailsPayload = z.infer<typeof problemDetailsSchema>;

/**
 * Converts RFC 6901 JSON Pointers (e.g., "/customer/taxId", "/payload/amount")
 * into dot-separated field keys matching UI form controls.
 */
export function jsonPointerToFieldKey(pointer: string): string {
  const cleaned = pointer.replace(/^#?\//, '').trim();
  if (!cleaned) return '_root';
  return cleaned
    .split('/')
    .map((segment) => segment.replace(/~1/g, '/').replace(/~0/g, '~'))
    .join('.');
}

export function mapPointerErrors(
  errors?: ProblemDetailsErrorItem[] | null,
): Record<string, string> {
  if (!errors || errors.length === 0) return {};
  const mapped: Record<string, string> = {};
  for (const item of errors) {
    const key = jsonPointerToFieldKey(item.pointer);
    if (!mapped[key]) mapped[key] = item.message;
  }
  return mapped;
}

export function parseRetryAfterSeconds(headerValue: string | null): number | undefined {
  if (!headerValue) return undefined;
  const trimmed = headerValue.trim();
  if (/^\d+$/.test(trimmed)) {
    const seconds = Number(trimmed);
    return Number.isFinite(seconds) && seconds >= 0 ? seconds : undefined;
  }
  const dateMs = Date.parse(trimmed);
  if (!Number.isNaN(dateMs)) {
    return Math.max(0, Math.ceil((dateMs - Date.now()) / 1000));
  }
  return undefined;
}
