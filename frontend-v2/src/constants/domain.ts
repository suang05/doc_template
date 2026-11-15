export const userRoles = ['Viewer', 'Developer', 'Admin'] as const;
export type UserRole = (typeof userRoles)[number];

export const systemRoles = ['Standard', 'SuperAdmin'] as const;
export type SystemRole = (typeof systemRoles)[number];

export const apiKeyScopes = ['ReadOnly', 'ReadWrite'] as const;
export type ApiKeyScope = (typeof apiKeyScopes)[number];

export const outputFormats = ['pdf', 'docx', 'xlsx'] as const;
export type OutputFormat = (typeof outputFormats)[number];

export const dataSourceTypes = ['json', 'sql'] as const;
export type DataSourceType = (typeof dataSourceTypes)[number];

export const databaseProviders = ['PostgreSQL', 'SqlServer', 'MySQL', 'Oracle'] as const;
export type DatabaseProvider = (typeof databaseProviders)[number];

export const documentJobStatuses = [
  'Queued',
  'Running',
  'Succeeded',
  'Failed',
  'Canceled',
] as const;
export type DocumentJobStatus = (typeof documentJobStatuses)[number];

export const TERMINAL_JOB_STATUSES: ReadonlySet<string> = new Set([
  'Succeeded',
  'Failed',
  'Canceled',
]);

export const generationStatuses = [
  'SUCCESS',
  'FAILED',
  'PROCESSING',
  'TIMEOUT',
  'VALIDATION_FAILED',
] as const;
export type GenerationStatus = (typeof generationStatuses)[number];

export function isSuccessGenerationStatus(status: string | null | undefined): boolean {
  return typeof status === 'string' && status.toUpperCase() === 'SUCCESS';
}

export const templateVersionStatuses = ['Draft', 'Published', 'Archived'] as const;
export type TemplateVersionStatus = (typeof templateVersionStatuses)[number];

export const ThaiTransformTypes = [
  { value: 'none', label: 'ไม่แปลง (Raw)' },
  { value: 'thai_date', label: 'วันที่ไทย (เช่น 12 เมษายน 2569)' },
  { value: 'thai_date_short', label: 'วันที่ไทยสั้น (เช่น 12/04/2569)' },
  { value: 'thai_baht_text', label: 'จำนวนเงินตัวอักษร (บาทถ้วน)' },
  { value: 'thai_currency', label: 'ตัวเลขเงิน 2 ทศนิยม (1,234.50)' },
  { value: 'thai_number', label: 'เลขไทย (๑๒๓๔)' },
  { value: 'thai_id_card', label: 'เลขบัตรประชาชน (x-xxxx-xxxxx-xx-x)' },
  { value: 'thai_phone', label: 'เบอร์โทรศัพท์ (0xx-xxx-xxxx)' },
  { value: 'uppercase', label: 'ตัวพิมพ์ใหญ่ (UPPERCASE)' },
  { value: 'lowercase', label: 'ตัวพิมพ์เล็ก (lowercase)' },
] as const;

export type ThaiTransformType = (typeof ThaiTransformTypes)[number]['value'];
