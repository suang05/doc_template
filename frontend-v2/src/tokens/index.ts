/**
 * SSoT Design Tokens for SAMMAKORN Document Platform v2
 * Strictly adheres to DESIGN.md. Zero arbitrary hex colors or radii permitted outside this token file.
 */

export const tokens = {
  radius: {
    none: '0px',
    sm: '2px',
    md: '4px',
  },
  colors: {
    canvas: '#f8fbfe',
    surface: '#ffffff',
    surfaceSubtle: '#f1f5f9',
    border: '#e2e8f0',
    borderFocus: '#bae6fd',
    primary: '#0284c7',
    primaryLight: '#f0f9ff',
    primaryDark: '#0369a1',
    textPrimary: '#0f172a',
    textSecondary: '#475569',
    textMuted: '#94a3b8',
  },
  categories: {
    contract: {
      id: 'contract',
      label: 'สัญญา/นิติกรรม',
      primary: '#6366f1',
      light: '#eef2ff',
      border: '#c7d2fe',
      text: '#4338ca',
    },
    financial: {
      id: 'financial',
      label: 'การเงิน/ใบเสร็จ',
      primary: '#10b981',
      light: '#ecfdf5',
      border: '#a7f3d0',
      text: '#065f46',
    },
    official: {
      id: 'official',
      label: 'หนังสือสำคัญ',
      primary: '#0284c7',
      light: '#f0f9ff',
      border: '#bae6fd',
      text: '#0369a1',
    },
    hr: {
      id: 'hr',
      label: 'บุคคล/ภายใน',
      primary: '#f59e0b',
      light: '#fffbeb',
      border: '#fde68a',
      text: '#92400e',
    },
    operations: {
      id: 'operations',
      label: 'ปฏิบัติการทั่วไป',
      primary: '#0891b2',
      light: '#ecfeff',
      border: '#a5f3fc',
      text: '#155e75',
    },
  },
  statuses: {
    success: {
      label: 'สำเร็จ',
      primary: '#10b981',
      light: '#ecfdf5',
      border: '#a7f3d0',
      text: '#065f46',
    },
    failed: {
      label: 'ล้มเหลว',
      primary: '#ef4444',
      light: '#fef2f2',
      border: '#fca5a5',
      text: '#991b1b',
    },
    pending: {
      label: 'กำลังดำเนินการ',
      primary: '#f59e0b',
      light: '#fffbeb',
      border: '#fde68a',
      text: '#92400e',
    },
    idle: {
      label: 'พร้อมใช้งาน',
      primary: '#64748b',
      light: '#f8fafc',
      border: '#cbd5e1',
      text: '#334155',
    },
  },
  formats: {
    pdf: {
      id: 'pdf',
      label: 'PDF',
      primary: '#e11d48',
      light: '#fff1f2',
      border: '#fecdd3',
      text: '#9f1239',
    },
    docx: {
      id: 'docx',
      label: 'Word',
      primary: '#2563eb',
      light: '#eff6ff',
      border: '#bfdbfe',
      text: '#1e40af',
    },
    xlsx: {
      id: 'xlsx',
      label: 'Excel',
      primary: '#059669',
      light: '#ecfdf5',
      border: '#a7f3d0',
      text: '#065f46',
    },
    html: {
      id: 'html',
      label: 'HTML',
      primary: '#d97706',
      light: '#fffbeb',
      border: '#fde68a',
      text: '#92400e',
    },
  },
} as const;

export type DocumentCategory = keyof typeof tokens.categories;
export type DocumentFormat = keyof typeof tokens.formats;
export type SystemStatus = keyof typeof tokens.statuses;
