// ─── API Response Types ────────────────────────────────────────────────────

export interface Project {
  id: string;
  code: string;
  name: string;
  description?: string;
  createdAt: string;
  apiKeys?: ApiKey[];
}

export interface ApiKey {
  id: string;
  projectId: string;
  name: string;
  keyMasked?: string;
  keySecret?: string;
  isActive: boolean;
  createdAt: string;
  expiresAt?: string;
  lastUsedAt?: string;
}

export interface TemplateVersion {
  id: string;
  templateId?: string;
  versionNumber: number;
  storagePath: string;
  fileSizeBytes: number;
  uploadedAt: string;
  note?: string;
}

export interface TemplateDetail {
  id?: string;
  fileName: string;
  originalName?: string;
  format?: string;
  storagePath: string;
  isGlobal: boolean;
  projectId?: string;
  projectCode?: string;
  createdAt?: string;
  uploadedAt?: string;
  version?: number;
  versionsCount?: number;
}

export interface TemplatesResponse {
  templates: string[];
  details: TemplateDetail[];
  count: number;
}

export interface TemplateVersionsResponse {
  fileName: string;
  versions: TemplateVersion[];
  count: number;
}

export interface TemplateVariable {
  key: string;
  type: "text" | "number" | "date" | "table" | "qrcode" | "barcode";
  label: string;
  columns?: string[];
}

export interface TemplateSchemaResponse {
  fileName: string;
  format: string;
  variables: TemplateVariable[];
  count: number;
  currentVersion?: number;
  versionsCount?: number;
  warnings?: string[];
  sampleData?: Record<string, unknown>;
}

export interface DocumentGenerationRequest {
  templateName: string;
  outputFormat: OutputFormat;
  data: Record<string, unknown>;
}

export interface DocumentGenerationResponse {
  message: string;
  downloadUrl: string;
  previewUrl: string;
  logId: string;
  previewObjectName: string;
  expiresIn: string;
}

export interface GenerationLog {
  id: string;
  createdAt: string;
  timestamp?: string;
  projectId?: string;
  apiKeyId?: string;
  templateName: string;
  outputFormat: string;
  status: GenerationStatus;
  executionTimeMs: number;
  outputFileName?: string;
  fileSizeBytes?: number;
  ipAddress?: string;
  userAgent?: string;
  errorMessage?: string;
  project?: { code: string; name: string };
  apiKey?: { name: string };
}

export interface LogsResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  logs: GenerationLog[];
}

export interface MetricSummary {
  totalGenerations: number;
  totalSuccess: number;
  totalFailed: number;
  successRatePercentage: number;
  avgExecutionTimeMs: number;
}

export type PlaceholderType = 'text' | 'table' | 'qrcode' | 'barcode' | 'image';

export interface FieldMappingItem {
  id?: string;
  placeholder: string;
  sourcePath: string;
  label: string;
  placeholderType?: PlaceholderType;
  transform?: string | null;
  defaultValue?: string | null;
  isRequired?: boolean;
  sortOrder?: number;
}

export interface FieldMappingsResponse {
  templateId: string;
  fileName: string;
  mappings: FieldMappingItem[];
}

export interface FieldMappingPreviewItem {
  placeholder: string;
  sourcePath: string;
  rawValue?: string | null;
  transformedValue?: string | null;
  transform?: string | null;
  found: boolean;
}

export interface FieldMappingPreviewResponse {
  fileName: string;
  preview: FieldMappingPreviewItem[];
}

export interface TiptapSaveRequest {
  fileName: string;
  content: string;
  isGlobal?: boolean;
}

export interface TiptapSaveResponse {
  message: string;
  fileName: string;
  storagePath: string;
  engineType: string;
}

export interface ReportBroSaveRequest {
  fileName: string;
  content: string;   // JSON string of the full ReportBro report definition
  isGlobal?: boolean;
}

export interface ReportBroSaveResponse {
  message: string;
  fileName: string;
  storagePath: string;
  engineType: string;
}

// ─── Literal Union Types (SSoT) ────────────────────────────────────────────

export type OutputFormat = "pdf" | "docx" | "xlsx" | "html";
export type GenerationStatus = "SUCCESS" | "FAILED";
export type TemplateFormat = "docx" | "xlsx";
export type VariableType = "text" | "number" | "date" | "table" | "qrcode" | "barcode";
export type ThaiTransform =
  | "formatThaiBaht"
  | "formatThaiDate"
  | "formatThaiDateTime"
  | "formatCurrency"
  | "formatPhone"
  | "formatThaiId";

// ─── UI State Types (SSoT) ─────────────────────────────────────────────────

export type InputMode = "form" | "json";
export type ModalSize = "sm" | "md" | "lg" | "full";
export type ButtonVariant = "primary" | "navy" | "outline" | "ghost" | "danger";
export type ButtonSize = "sm" | "md" | "lg";

export type FormatType  = "docx" | "xlsx" | "pdf" | "html" | "json";
export type StatusType  = "success" | "failed" | "processing";
export type RoleType    = "DEV" | "ADMIN";
export type PillIntent  = StatusType | RoleType | "count";
export type BadgeIntent = FormatType | "grey" | "blue" | "violet";

export interface NavItem {
  id: string;
  label: string;
  icon?: string;
  soon?: boolean;
  badge?: number;
  children?: NavItem[];
}

export interface TableColumn<T = object> {
  key: string;
  header: string;
  width?: string;
  render?: (row: T) => React.ReactNode;
}

export interface SelectOption {
  value: string;
  label: string;
}

export interface AuditFilters {
  projectId: string;
  status: "" | GenerationStatus;
  page: number;
  pageSize: number;
}
