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

/**
 * Download name for formatted CV PDF: `{PersonName}-CV.pdf` (matches server
 * Content-Disposition). Falls back to `CV.pdf` when the person name is missing.
 */
export function buildCvExportDownloadFileName(personName: string | null | undefined): string {
  const personSegment = sanitizeCvExportFileNameSegment(personName);
  const base = personSegment ? `${personSegment}-CV` : 'CV';
  return `${base}.pdf`;
}
