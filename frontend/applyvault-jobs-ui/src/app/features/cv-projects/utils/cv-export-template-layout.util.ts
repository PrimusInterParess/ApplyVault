import { CvSectionType, CvStructuredSection } from '../models/cv-structured.model';
import {
  contactEntryValue,
  isContactNameEntry
} from './cv-contact-channels.util';

export type CvExportPreviewZone = 'header' | 'sidebar' | 'main';

export interface CvExportPreviewLayout {
  readonly header: readonly CvStructuredSection[];
  readonly sidebar: readonly CvStructuredSection[];
  readonly main: readonly CvStructuredSection[];
}

function isContactSection(section: CvStructuredSection): boolean {
  return section.heading.trim().toLowerCase() === 'contact' || section.sectionType === 'Contact';
}

function isSidebarSection(section: CvStructuredSection): boolean {
  return (
    section.sectionType === 'Skills' ||
    section.sectionType === 'Summary' ||
    isContactSection(section)
  );
}

function isHeaderSection(section: CvStructuredSection): boolean {
  return section.sectionType === 'Summary' || isContactSection(section);
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

function sectionHasRenderableContent(section: CvStructuredSection): boolean {
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

export function partitionSectionsForTemplate(
  templateId: number,
  sections: readonly CvStructuredSection[],
  options?: { includeEmpty?: boolean }
): CvExportPreviewLayout {
  const includeEmpty = options?.includeEmpty ?? false;
  const ordered = sortSections(sections).filter(
    (section) => includeEmpty || sectionHasRenderableContent(section) || section.entries.length > 0
  );

  // Minimal (3): Contact + Summary in header (match CvExportHtmlMapper).
  if (templateId === 3) {
    const header: CvStructuredSection[] = [];
    const main: CvStructuredSection[] = [];

    for (const section of ordered) {
      if (isHeaderSection(section)) {
        header.push(section);
      } else {
        main.push(section);
      }
    }

    return { header, sidebar: [], main };
  }

  const sidebar: CvStructuredSection[] = [];
  const main: CvStructuredSection[] = [];

  for (const section of ordered) {
    if (isSidebarSection(section)) {
      sidebar.push(section);
    } else {
      main.push(section);
    }
  }

  return { header: [], sidebar, main };
}
