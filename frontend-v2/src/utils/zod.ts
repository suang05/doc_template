import { z } from 'zod';

/**
 * Flatten Zod validation issues into a dot-path keyed error map
 * for inline form field feedback.
 */
export function toFieldErrorMap(error: z.ZodError): Record<string, string> {
  const result: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = issue.path.join('.') || '_root';
    if (!result[key]) {
      result[key] = issue.message;
    }
  }
  return result;
}
