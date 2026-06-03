export interface CvExportTemplateOption {
  readonly id: number;
  readonly label: string;
}

export interface CvExportMaxPageOption {
  readonly value: number | null;
  readonly label: string;
}

export const CV_EXPORT_TEMPLATES: readonly CvExportTemplateOption[] = [
  { id: 1, label: 'Classic' },
  { id: 2, label: 'Modern (two-column)' },
  { id: 3, label: 'Minimal ATS' },
  { id: 4, label: 'Creative' },
  { id: 5, label: 'Professional (single-column)' }
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
