import { z } from 'zod';

export const projectSlugRegex = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export const createProjectSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, 'ชื่อโปรเจกต์ต้องมีอย่างน้อย 2 ตัวอักษร')
    .max(120, 'ชื่อโปรเจกต์ต้องไม่เกิน 120 ตัวอักษร'),
  slug: z
    .string()
    .trim()
    .min(2, 'Slug ต้องมีอย่างน้อย 2 ตัวอักษร')
    .max(64, 'Slug ต้องไม่เกิน 64 ตัวอักษร')
    .regex(
      projectSlugRegex,
      'Slug ต้องเป็นตัวพิมพ์เล็ก ตัวเลข หรือขีดกลาง (-) เช่น billing-core',
    ),
});

export type CreateProjectFormValues = z.infer<typeof createProjectSchema>;

export const transferProjectOwnerSchema = z.object({
  newOwnerUserId: z
    .string()
    .trim()
    .uuid('กรุณาระบุ New Owner User ID ในรูปแบบ UUID ที่ถูกต้อง'),
});

const ipv4CidrRegex =
  /^(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){3}\/(3[0-2]|[12]?\d)$/;

export function parseAndValidateEgressCidrs(rawInput: string): {
  cidrs: string[];
  error?: string;
} {
  const entries = rawInput
    .split(/[\n,;]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);

  const uniqueCidrs = Array.from(new Set(entries));

  for (const cidr of uniqueCidrs) {
    if (!ipv4CidrRegex.test(cidr) && !cidr.includes(':')) {
      return {
        cidrs: [],
        error: `รูปแบบ CIDR ไม่ถูกต้อง: "${cidr}" (ตัวอย่าง: 10.0.0.0/8 หรือ 192.168.1.0/24)`,
      };
    }
  }

  return { cidrs: uniqueCidrs };
}

export const upsertProjectQuotaSchema = z.object({
  maxRequestsPerMinute: z.coerce
    .number()
    .int('ต้องเป็นจำนวนเต็ม')
    .min(1, 'ต้องมากกว่าหรือเท่ากับ 1')
    .max(1_000_000, 'ต้องไม่เกิน 1,000,000'),
  maxConcurrentJobs: z.coerce
    .number()
    .int('ต้องเป็นจำนวนเต็ม')
    .min(1, 'ต้องมากกว่าหรือเท่ากับ 1')
    .max(10_000, 'ต้องไม่เกิน 10,000'),
  maxBatchItems: z.coerce
    .number()
    .int('ต้องเป็นจำนวนเต็ม')
    .min(1, 'ต้องมากกว่าหรือเท่ากับ 1')
    .max(100_000, 'ต้องไม่เกิน 100,000'),
  maxStorageBytes: z.coerce
    .number()
    .int('ต้องเป็นจำนวนเต็ม')
    .min(1, 'ต้องมากกว่า 0 bytes'),
  maxActiveApiKeys: z.coerce
    .number()
    .int('ต้องเป็นจำนวนเต็ม')
    .min(1, 'ต้องมากกว่าหรือเท่ากับ 1')
    .max(100, 'ต้องไม่เกิน 100'),
  isActive: z.boolean(),
});

export type UpsertProjectQuotaFormValues = z.infer<typeof upsertProjectQuotaSchema>;

export const grantBreakGlassSchema = z.object({
  targetProjectId: z.string().uuid('กรุณาเลือกโปรเจกต์เป้าหมาย'),
  reason: z
    .string()
    .trim()
    .min(5, 'กรุณาระบุเหตุผลการเข้าถึงอย่างน้อย 5 ตัวอักษร')
    .max(500, 'เหตุผลต้องไม่เกิน 500 ตัวอักษร'),
  superAdminUserId: z
    .string()
    .trim()
    .optional()
    .refine(
      (val) => !val || z.string().uuid().safeParse(val).success,
      'SuperAdmin User ID ต้องเป็น UUID ที่ถูกต้อง',
    ),
  durationMinutes: z.coerce
    .number()
    .int('ต้องเป็นจำนวนเต็ม')
    .min(1, 'ระยะเวลาอย่างน้อย 1 นาที')
    .max(1440, 'ระยะเวลาต้องไม่เกิน 1,440 นาที'),
});

export const registerPlatformFontSchema = z.object({
  familyName: z.string().trim().max(120, 'ชื่อฟอนต์ต้องไม่เกิน 120 ตัวอักษร').optional(),
  weight: z.string().trim().min(1, 'กรุณาเลือกน้ำหนักฟอนต์'),
  style: z.enum(['normal', 'italic', 'oblique']),
});

export const FONT_WEIGHT_OPTIONS = [
  { value: 'normal', label: 'Normal (400)' },
  { value: 'bold', label: 'Bold (700)' },
  { value: '100', label: 'Thin (100)' },
  { value: '300', label: 'Light (300)' },
  { value: '500', label: 'Medium (500)' },
  { value: '600', label: 'SemiBold (600)' },
  { value: '800', label: 'ExtraBold (800)' },
  { value: '900', label: 'Black (900)' },
] as const;

export const FONT_STYLE_OPTIONS = [
  { value: 'normal', label: 'Normal' },
  { value: 'italic', label: 'Italic' },
  { value: 'oblique', label: 'Oblique' },
] as const;

export const MAX_PLATFORM_FONT_BYTES = 8 * 1024 * 1024;
export const ACCEPTED_FONT_EXTENSIONS = ['.ttf', '.otf', '.woff', '.woff2'] as const;

export function slugifyProjectName(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64);
}
