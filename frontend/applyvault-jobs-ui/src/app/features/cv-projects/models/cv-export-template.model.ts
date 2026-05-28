export interface CvExportTemplateOption {
  readonly id: number;
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
