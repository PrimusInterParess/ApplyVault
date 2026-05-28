import { moveItemInArray } from '@angular/cdk/drag-drop';

import {
  CvStructuredEntry,
  CvStructuredSection,
  CvStructuredEntryWrite,
  CvStructuredSectionWrite,
  SaveCvStructuredDocumentRequest
} from '../models/cv-structured.model';

export function cloneSectionsForDraft(
  sections: readonly CvStructuredSection[]
): CvStructuredSection[] {
  return sections.map((section) => cloneSectionForDraft(section));
}

export function cloneSectionForDraft(section: CvStructuredSection): CvStructuredSection {
  return {
    ...section,
    entries: section.entries.map((entry) => ({
      ...entry,
      bullets: [...entry.bullets]
    }))
  };
}

export function normalizeSectionSortOrders(sections: CvStructuredSection[]): void {
  sections.forEach((section, index) => {
    section.sortOrder = index;
    normalizeEntrySortOrders(section.entries);
  });
}

export function normalizeEntrySortOrders(entries: CvStructuredEntry[]): void {
  entries.forEach((entry, index) => {
    entry.sortOrder = index;
  });
}

export function createEmptySection(sortOrder: number): CvStructuredSection {
  return {
    id: crypto.randomUUID(),
    heading: 'New section',
    sectionType: 'Custom',
    sortOrder,
    entries: []
  };
}

export function updateSectionHeading(section: CvStructuredSection, heading: string): CvStructuredSection {
  return {
    ...section,
    heading
  };
}

export function updateEntryField(
  section: CvStructuredSection,
  entryId: string,
  patch: Partial<Pick<CvStructuredEntry, 'title' | 'subtitle' | 'dateRange' | 'summary' | 'bullets' | 'techStack'>>
): CvStructuredSection {
  return {
    ...section,
    entries: section.entries.map((entry) =>
      entry.id === entryId
        ? {
            ...entry,
            ...patch,
            bullets: patch.bullets ? [...patch.bullets] : entry.bullets
          }
        : entry
    )
  };
}

export function addEntryToSection(section: CvStructuredSection): CvStructuredSection {
  const nextEntries = [
    ...section.entries.map((entry) => ({ ...entry, bullets: [...entry.bullets] })),
    createEmptyEntry(section.entries.length)
  ];

  normalizeEntrySortOrders(nextEntries);

  return {
    ...section,
    entries: nextEntries
  };
}

export function removeEntryFromSection(section: CvStructuredSection, entryId: string): CvStructuredSection {
  const nextEntries = section.entries
    .filter((entry) => entry.id !== entryId)
    .map((entry) => ({ ...entry, bullets: [...entry.bullets] }));

  normalizeEntrySortOrders(nextEntries);

  return {
    ...section,
    entries: nextEntries
  };
}

export function moveEntryInSection(
  section: CvStructuredSection,
  fromIndex: number,
  toIndex: number
): CvStructuredSection {
  const nextEntries = section.entries.map((entry) => ({ ...entry, bullets: [...entry.bullets] }));

  moveItemInArray(nextEntries, fromIndex, toIndex);
  normalizeEntrySortOrders(nextEntries);

  return {
    ...section,
    entries: nextEntries
  };
}

export function entryHasContent(entry: CvStructuredEntry): boolean {
  return (
    entry.title.trim().length > 0 ||
    (entry.subtitle?.trim().length ?? 0) > 0 ||
    (entry.dateRange?.trim().length ?? 0) > 0 ||
    entry.summary.trim().length > 0 ||
    entry.bullets.some((bullet) => bullet.trim().length > 0) ||
    entry.techStack.trim().length > 0
  );
}

export function createEmptyEntry(sortOrder: number): CvStructuredEntry {
  return {
    id: crypto.randomUUID(),
    title: '',
    subtitle: null,
    dateRange: null,
    summary: '',
    bullets: [],
    techStack: '',
    source: 'Manual',
    sourceSummaryId: null,
    sortOrder
  };
}

export function sectionsAreEqual(
  left: readonly CvStructuredSection[],
  right: readonly CvStructuredSection[]
): boolean {
  return JSON.stringify(toComparableSections(left)) === JSON.stringify(toComparableSections(right));
}

export function sectionEquals(
  left: CvStructuredSection,
  right: CvStructuredSection
): boolean {
  return JSON.stringify(toComparableSection(left)) === JSON.stringify(toComparableSection(right));
}

export function mergeSection(
  sections: readonly CvStructuredSection[],
  updatedSection: CvStructuredSection
): CvStructuredSection[] {
  const existingIndex = sections.findIndex((section) => section.id === updatedSection.id);

  if (existingIndex >= 0) {
    return sections.map((section) =>
      section.id === updatedSection.id ? cloneSectionForDraft(updatedSection) : cloneSectionForDraft(section)
    );
  }

  return [...sections.map((section) => cloneSectionForDraft(section)), cloneSectionForDraft(updatedSection)];
}

export function reorderSections(
  sections: readonly CvStructuredSection[],
  fromIndex: number,
  toIndex: number
): CvStructuredSection[] {
  const nextSections = sections.map((section) => cloneSectionForDraft(section));

  moveItemInArray(nextSections, fromIndex, toIndex);
  normalizeSectionSortOrders(nextSections);
  return nextSections;
}

export function removeSectionById(
  sections: readonly CvStructuredSection[],
  sectionId: string
): CvStructuredSection[] {
  const nextSections = sections
    .filter((section) => section.id !== sectionId)
    .map((section) => cloneSectionForDraft(section));

  normalizeSectionSortOrders(nextSections);
  return nextSections;
}

export function toSaveRequest(sections: readonly CvStructuredSection[]): SaveCvStructuredDocumentRequest {
  return {
    sections: sections.map((section, sectionIndex) => toSectionWrite(section, sectionIndex))
  };
}

function toSectionWrite(section: CvStructuredSection, sortOrder: number): CvStructuredSectionWrite {
  return {
    id: section.id,
    heading: section.heading.trim(),
    sectionType: section.sectionType,
    sortOrder,
    entries: section.entries.map((entry, entryIndex) => toEntryWrite(entry, entryIndex))
  };
}

function toEntryWrite(entry: CvStructuredEntry, sortOrder: number): CvStructuredEntryWrite {
  return {
    id: entry.id,
    title: entry.title.trim(),
    subtitle: normalizeOptionalText(entry.subtitle),
    dateRange: normalizeOptionalText(entry.dateRange),
    summary: entry.summary.trim(),
    bullets: entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0),
    techStack: entry.techStack.trim(),
    source: entry.source.trim() || 'Manual',
    sourceSummaryId: entry.sourceSummaryId,
    sortOrder
  };
}

function normalizeOptionalText(value: string | null): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

function toComparableSections(sections: readonly CvStructuredSection[]) {
  return [...sections]
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((section) => toComparableSection(section));
}

function toComparableSection(section: CvStructuredSection) {
  return {
    id: section.id,
    heading: section.heading.trim(),
    sectionType: section.sectionType,
    sortOrder: section.sortOrder,
    entries: [...section.entries]
      .sort((left, right) => left.sortOrder - right.sortOrder)
      .map((entry) => ({
        id: entry.id,
        title: entry.title.trim(),
        subtitle: normalizeOptionalText(entry.subtitle),
        dateRange: normalizeOptionalText(entry.dateRange),
        summary: entry.summary.trim(),
        bullets: entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0),
        techStack: entry.techStack.trim(),
        source: entry.source.trim() || 'Manual',
        sourceSummaryId: entry.sourceSummaryId,
        sortOrder: entry.sortOrder
      }))
  };
}
