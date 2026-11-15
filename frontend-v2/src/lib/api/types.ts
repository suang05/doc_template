import type { components } from './schema';

type SchemaOf<Name extends keyof components['schemas']> =
  components['schemas'][Name];

// Auth & User Profile
export type LoginRequest = SchemaOf<'LoginRequest'>;
export type LoginResultDto = SchemaOf<'LoginResultDto'>;
export type TokenResultDto = SchemaOf<'TokenResultDto'>;

// Templates & Studio
export type TemplateDto = SchemaOf<'TemplateResultDto'>;
export type UpdateTemplateMetadataRequest = SchemaOf<'UpdateTemplateMetadataRequest'>;
export type TemplateStudioDto = SchemaOf<'TemplateStudioDto'>;
export type SaveHtmlRequest = SchemaOf<'SaveHtmlRequest'>;
export type SaveHtmlResultDto = SchemaOf<'SaveHtmlResultDto'>;
export type RollbackVersionResultDto = SchemaOf<'RollbackVersionResultDto'>;
export type TemplateVersionDto = SchemaOf<'TemplateVersionDto'>;
export type TemplateValidationResultDto = SchemaOf<'TemplateValidationResultDto'>;
export type TemplateSchemaDto = SchemaOf<'TemplateSchemaDto'>;
export type ScanFieldsResultDto = SchemaOf<'ScanFieldsResultDto'>;
export type ParseDraftResultDto = SchemaOf<'ParseDraftResultDto'>;
export type CommitDraftRequest = SchemaOf<'CommitDraftRequest'>;
export type CommitDraftResultDto = SchemaOf<'CommitDraftResultDto'>;

// Field Mappings & Template Datasets
export type FieldMappingDto = SchemaOf<'FieldMappingResultDto'>;
export type SaveFieldMappingItem = SchemaOf<'SaveFieldMappingItemRequest'>;
export type SaveTemplateMappingsRequest = SchemaOf<'SaveTemplateMappingsRequest'>;
export type TemplateDatasetDto = SchemaOf<'TemplateDatasetResultDto'>;
export type SaveTemplateDatasetItem = SchemaOf<'SaveTemplateDatasetItemRequest'>;
export type SaveTemplateDatasetsRequest = SchemaOf<'SaveTemplateDatasetsRequest'>;

// Data Sources & Datasets
export type DataConnectionDto = SchemaOf<'DataConnectionResultDto'>;
export type CreateDataConnectionRequest = SchemaOf<'CreateDataConnectionRequest'>;
export type UpdateDataConnectionRequest = SchemaOf<'UpdateDataConnectionRequest'>;
export type TestDataConnectionRequest = SchemaOf<'TestDataConnectionRequest'>;
export type TestDataConnectionResultDto = SchemaOf<'TestDataConnectionResultDto'>;
export type DatasetDto = SchemaOf<'DatasetResultDto'>;
export type DatasetColumnSchemaDto = SchemaOf<'DatasetColumnSchemaDto'>;
export type CreateDatasetRequest = SchemaOf<'CreateDatasetRequest'>;
export type UpdateDatasetRequest = SchemaOf<'UpdateDatasetRequest'>;
export type DryRunDatasetRequest = SchemaOf<'DryRunDatasetRequest'>;
export type DryRunDatasetResultDto = SchemaOf<'DryRunDatasetResultDto'>;

// API Keys & Users
export type ApiKeyDto = SchemaOf<'ApiKeyResultDto'>;
export type CreateApiKeyRequest = SchemaOf<'CreateApiKeyRequest'>;
export type CreateApiKeyResultDto = SchemaOf<'CreateApiKeyResultDto'>;
export type UserDto = SchemaOf<'UserResultDto'>;
export type InviteUserRequest = SchemaOf<'InviteUserRequest'>;
export type UpdateUserRoleRequest = SchemaOf<'UpdateUserRoleRequest'>;
export type SetUserStatusRequest = SchemaOf<'SetUserStatusRequest'>;

// Control Plane (/management & /portal)
export type ProjectDto = SchemaOf<'ProjectResultDto'>;
export type ProjectControlPlaneDto = SchemaOf<'ProjectControlPlaneResultDto'>;
export type CreateProjectRequest = SchemaOf<'CreateProjectRequest'>;
export type UpdateProjectEgressCidrsRequest = SchemaOf<'UpdateProjectEgressCidrsRequest'>;
export type ProjectQuotaDto = SchemaOf<'ProjectQuotaResultDto'>;
export type UpsertProjectQuotaRequest = SchemaOf<'UpsertProjectQuotaRequest'>;
export type BreakGlassGrantDto = SchemaOf<'BreakGlassGrantResultDto'>;
export type GrantBreakGlassAccessRequest = SchemaOf<'GrantBreakGlassAccessRequest'>;
export type PlatformFontDto = SchemaOf<'PlatformFontResultDto'>;
export type UploadFontResultDto = SchemaOf<'UploadFontResultDto'>;
export type FontCatalogDto = SchemaOf<'FontCatalogDto'>;
export type FontDescriptorDto = SchemaOf<'FontDescriptorDto'>;

// Pipeline B/C Documents, Jobs & Audit Logs
export type CreateDocumentRequest = SchemaOf<'CreateDocumentRequest'>;
export type GenerateDocumentRequest = SchemaOf<'GenerateDocumentRequest'>;
export type GenerateDocumentResultDto = SchemaOf<'GenerateDocumentResultDto'>;
export type PreviewDocumentRequest = SchemaOf<'PreviewDocumentRequest'>;
export type DocumentJobResultDto = SchemaOf<'DocumentJobResultDto'>;
export type DocumentJobItemDto = SchemaOf<'DocumentJobItemDto'>;
export type DocumentJobListResult = SchemaOf<'DocumentJobListResult'>;
export type CreateDocumentJobRequest = SchemaOf<'CreateDocumentJobRequest'>;
export type CreateDocumentJobItemRequest = SchemaOf<'CreateDocumentJobItemRequest'>;
export type DocumentVersionDto = SchemaOf<'DocumentVersionDto'>;
export type GenerationLogDto = SchemaOf<'GenerationLogDto'>;
export type LogMetricsDto = SchemaOf<'LogMetricsDto'>;
export type LogPayloadResultDto = SchemaOf<'LogPayloadResultDto'>;
export type PaginationMetadata = SchemaOf<'PaginationMetadata'>;

export interface ApiEnvelope<T> {
  data: T;
}

export interface PagedEnvelope<T> {
  data: T[];
  pagination: PaginationMetadata;
}

export interface ResourceWithETag<T> {
  data: T;
  etag: string | null;
  headers: Headers;
}
