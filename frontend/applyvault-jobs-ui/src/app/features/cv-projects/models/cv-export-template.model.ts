export type CvExportTemplateLayoutKind = 'twoColumn' | 'minimal';

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

/** Gallery: Modern and Minimal (ids 2–3). Legacy Classic (1) remaps to Modern. */
export const CV_EXPORT_TEMPLATES: readonly CvExportTemplateOption[] = [
  {
    id: 2,
    label: 'Modern',
    description: 'Two-column layout with a soft sidebar and open main column.',
    layoutKind: 'twoColumn'
  },
  {
    id: 3,
    label: 'Minimal',
    description: 'Sparse one-column layout with light rules and open whitespace.',
    layoutKind: 'minimal'
  }
] as const;

export const MAX_CV_EXPORT_TEMPLATE_ID = CV_EXPORT_TEMPLATES[CV_EXPORT_TEMPLATES.length - 1].id;

export const DEFAULT_CV_EXPORT_TEMPLATE_ID = 2;

export const CV_EXPORT_TEMPLATE_STORAGE_KEY = 'applyvault.cvExportTemplateId';

export const CV_EXPORT_MAX_PAGE_OPTIONS: readonly CvExportMaxPageOption[] = [
  { value: null, label: 'No limit' },
  { value: 1, label: '1 page' },
  { value: 2, label: '2 pages' }
] as const;

/** Default export target: fit to a single page (BE compact ramp). */
export const DEFAULT_CV_EXPORT_MAX_PAGES: number | null = 1;

export const CV_EXPORT_MAX_PAGES_STORAGE_KEY = 'applyvault.cvExportMaxPages';

/**
 * Normalize template ids to the supported set.
 * Keep 2|3; map legacy Classic (1), 4/5, and any unknown → Modern (2).
 */
export function normalizeCvExportTemplateId(templateId: number): number {
  return templateId === 2 || templateId === 3 ? templateId : DEFAULT_CV_EXPORT_TEMPLATE_ID;
}
