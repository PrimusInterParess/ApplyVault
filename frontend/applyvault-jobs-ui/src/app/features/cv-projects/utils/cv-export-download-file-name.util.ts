import { CV_EXPORT_TEMPLATES } from '../models/cv-export-template.model';
import { CvStructuredSection } from '../models/cv-structured.model';
import { findContactNameEntry } from './cv-contact-channels.util';

const INVALID_FILE_NAME_CHARS = /[<>:"/\\|?*\u0000-\u001f]/g;
const COLLAPSE_WHITESPACE = /\s+/g;
const COLLAPSE_HYPHENS = /-+/g;

/**
 * Builds a safe download basename segment from free text (spaces → hyphens).
 * Returns empty string when nothing usable remains.
 */
export function sanitizeCvExportFileNameSegment(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  return value
    .normalize('NFKC')
    .trim()
    .replace(COLLAPSE_WHITESPACE, '-')
    .replace(INVALID_FILE_NAME_CHARS, '')
    .replace(COLLAPSE_HYPHENS, '-')
    .replace(/^-|-$/g, '');
}

/** Person name from Contact for download filenames (not the "Your name" UI placeholder). */
export function resolveCvExportPersonName(
  sections: readonly CvStructuredSection[] | null | undefined
): string | null {
  if (!sections?.length) {
    return null;
  }

  const contact = sections.find(
    (section) =>
      section.sectionType === 'Contact' || section.heading.trim().toLowerCase() === 'contact'
  );

  if (!contact) {
    return null;
  }

  const name = findContactNameEntry(contact)?.subtitle?.trim();
  return name || null;
}

export function resolveCvExportTemplateLabel(templateId: number): string {
  return CV_EXPORT_TEMPLATES.find((template) => template.id === templateId)?.label ?? 'Modern';
}

/**
 * Download name for formatted CV PDF: `{PersonName}-{TemplateLabel}.pdf`.
 * Falls back to `CV-{TemplateLabel}.pdf` when the person name is missing.
 */
export function buildCvExportDownloadFileName(
  personName: string | null | undefined,
  templateLabel: string
): string {
  const templateSegment = sanitizeCvExportFileNameSegment(templateLabel) || 'Modern';
  const personSegment = sanitizeCvExportFileNameSegment(personName);
  const base = personSegment ? `${personSegment}-${templateSegment}` : `CV-${templateSegment}`;
  return `${base}.pdf`;
}
