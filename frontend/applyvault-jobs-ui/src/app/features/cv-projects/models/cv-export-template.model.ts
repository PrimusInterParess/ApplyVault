export type CvExportTemplateLayoutKind = 'classic' | 'twoColumn' | 'minimal';

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

/** M1 gallery: Classic, Modern, Minimal only (ids 1–3). */
export const CV_EXPORT_TEMPLATES: readonly CvExportTemplateOption[] = [
  {
    id: 1,
    label: 'Classic',
    description: 'One-column layout with a clear header band.',
    layoutKind: 'classic'
  },
  {
    id: 2,
    label: 'Modern',
    description: 'Simple two-column layout with sidebar and main body.',
    layoutKind: 'twoColumn'
  },
  {
    id: 3,
    label: 'Minimal',
    description: 'Clean one-column layout with sparse styling.',
    layoutKind: 'minimal'
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

/**
 * Normalize template ids to the M1 supported set.
 * Keep 2|3; map legacy 4/5 and any unknown → Classic (1).
 */
export function normalizeCvExportTemplateId(templateId: number): number {
  return templateId === 2 || templateId === 3 ? templateId : DEFAULT_CV_EXPORT_TEMPLATE_ID;
}
