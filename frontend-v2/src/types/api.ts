export * from '../tokens';
export * from '../schemas/document.schema';
export * from '../schemas/template.schema';
export * from '../schemas/mapping.schema';
export * from '../schemas/templateDataset.schema';
export * from '../schemas/apikey.schema';
export * from '../schemas/log.schema';
export * from '../schemas/dataset.schema';
export * from '../schemas/dataconnection.schema';
export * from '../schemas/health.schema';

export interface ApiResponse<T> {
  data?: T;
  error?: string;
  success?: boolean;
}
export interface RenderHtmlToPdfRequest {
  htmlContent: string;
  headerHtml?: string;
  footerHtml?: string;
}
