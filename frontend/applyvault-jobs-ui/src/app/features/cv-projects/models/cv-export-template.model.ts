export type CvExportTemplateLayoutKind =
  | 'classic'
  | 'twoColumn'
  | 'minimal'
  | 'creative'
  | 'professional';

export interface CvExportTemplateOption {
  readonly id: number;
  readonly label: string;
  readonly description: string;
  readonly layoutKind: CvExportTemplateLayoutKind;
}

export interface CvExportMaxPageOption {
  readonly value: number | null;
  readonly label: string;
}

export const CV_EXPORT_TEMPLATES: readonly CvExportTemplateOption[] = [
  {
    id: 1,
    label: 'Classic',
    description: 'Single-column layout with a clear header band and navy accents.',
    layoutKind: 'classic'
  },
  {
    id: 2,
    label: 'Modern (two-column)',
    description: 'Dark sidebar for contact and skills; experience in the main column.',
    layoutKind: 'twoColumn'
  },
  {
    id: 3,
    label: 'Minimal ATS',
    description: 'Serif type and simple rules — optimized for applicant tracking systems.',
    layoutKind: 'minimal'
  },
  {
    id: 4,
    label: 'Creative',
    description: 'Purple gradient sidebar with rounded photo and accent borders.',
    layoutKind: 'creative'
  },
  {
    id: 5,
    label: 'Professional (single-column)',
    description: 'Name and contact block at the top; Calibri-style body sections below.',
    layoutKind: 'professional'
  }
] as const;

export const MAX_CV_EXPORT_TEMPLATE_ID = CV_EXPORT_TEMPLATES[CV_EXPORT_TEMPLATES.length - 1].id;

export const DEFAULT_CV_EXPORT_TEMPLATE_ID = 1;

export const CV_EXPORT_TEMPLATE_STORAGE_KEY = 'applyvault.cvExportTemplateId';

export const CV_EXPORT_MAX_PAGE_OPTIONS: readonly CvExportMaxPageOption[] = [
  { value: null, label: 'No limit' },
  { value: 1, label: '1 page' },
  { value: 2, label: '2 pages' }
] as const;

export const DEFAULT_CV_EXPORT_MAX_PAGES: number | null = null;

export const CV_EXPORT_MAX_PAGES_STORAGE_KEY = 'applyvault.cvExportMaxPages';
