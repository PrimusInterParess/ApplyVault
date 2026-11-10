import { CvStructuredSection } from '../models/cv-structured.model';
import {
  contactEntryValue,
  isContactNameEntry
} from './cv-contact-channels.util';

export type CvExportPreviewZone = 'header' | 'main';

export interface CvExportPreviewLayout {
  readonly header: readonly CvStructuredSection[];
  readonly main: readonly CvStructuredSection[];
}

function isContactSection(section: CvStructuredSection): boolean {
  return section.heading.trim().toLowerCase() === 'contact' || section.sectionType === 'Contact';
}

function isHeaderSection(section: CvStructuredSection): boolean {
  return isContactSection(section);
}

function sortSections(sections: readonly CvStructuredSection[]): CvStructuredSection[] {
  return [...sections].sort((left, right) => left.sortOrder - right.sortOrder);
}

/** Contact export/preview: labels alone do not count as content. */
export function contactSectionHasRenderableContent(section: CvStructuredSection): boolean {
  return section.entries.some((entry) => {
    if (isContactNameEntry(entry)) {
      return (entry.subtitle?.trim().length ?? 0) > 0;
    }

    return contactEntryValue(entry).length > 0;
  });
}

/** True when a Section has any non-empty Entry field (export + rail completeness). */
export function sectionHasRenderableContent(section: CvStructuredSection): boolean {
  if (isContactSection(section)) {
    return contactSectionHasRenderableContent(section);
  }

  return section.entries.some(
    (entry) =>
      entry.title.trim().length > 0 ||
      (entry.subtitle?.trim().length ?? 0) > 0 ||
      (entry.dateRange?.trim().length ?? 0) > 0 ||
      entry.summary.trim().length > 0 ||
      entry.bullets.some((bullet) => bullet.trim().length > 0) ||
      entry.techStack.trim().length > 0
  );
}

/**
 * Single export layout (match CvExportHtmlMapper): Contact in the header,
 * Summary and every other section in the single main column.
 * Non-edit and edit-canvas callers drop a non-Contact Section when
 * `includedOnTemplate` is false. The edit canvas still shows empty Sections.
 */
export function partitionSectionsForExport(
  sections: readonly CvStructuredSection[],
  options?: { includeEmpty?: boolean; editCanvas?: boolean }
): CvExportPreviewLayout {
  const includeEmpty = options?.includeEmpty ?? false;
  const ordered = sortSections(sections).filter((section) => {
    if (section.includedOnTemplate === false && !isContactSection(section)) {
      return false;
    }

    return includeEmpty || sectionHasRenderableContent(section) || section.entries.length > 0;
  });

  const header: CvStructuredSection[] = [];
  const main: CvStructuredSection[] = [];

  for (const section of ordered) {
    if (isHeaderSection(section)) {
      header.push(section);
    } else {
      main.push(section);
    }
  }

  return { header, main };
}
