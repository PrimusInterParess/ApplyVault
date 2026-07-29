import { CvSectionType, CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import {
  createContactFieldEntry,
  createStarterContactEntries
} from './cv-contact-channels.util';
import {
  createEmptyEntry,
  createEmptySection,
  normalizeEntrySortOrders,
  normalizeSectionSortOrders
} from './cv-structured-draft.util';

export { createStarterContactEntries };

const BULLET_SLOT_SECTION_TYPES = new Set<CvSectionType>([
  'Experience',
  'Education',
  'Projects'
]);

const DEFAULT_SECTION_HEADINGS: Record<CvSectionType, string> = {
  Contact: 'Contact',
  Summary: 'Summary',
  Experience: 'Experience',
  Education: 'Education',
  Skills: 'Skills',
  Projects: 'Projects',
  Custom: 'Custom'
};

/**
 * Starter Entry for a Section type — shared by Blank CV start and later add Entry.
 * Contact adds are unlabeled empty fields; blank-start Contact uses createStarterContactEntries.
 */
export function createStarterEntryForSection(
  sectionType: CvSectionType,
  sortOrder: number
): CvStructuredEntry {
  if (sectionType === 'Contact') {
    return createContactFieldEntry(sortOrder);
  }

  if (BULLET_SLOT_SECTION_TYPES.has(sectionType)) {
    return {
      ...createEmptyEntry(sortOrder),
      bullets: ['']
    };
  }

  // Skills UI is a comma skills/techStack input — empty Entry + field placeholder is the slot.
  return createEmptyEntry(sortOrder);
}

export function addStarterEntryToSection(section: CvStructuredSection): CvStructuredSection {
  const nextEntries = [
    ...section.entries.map((entry) => ({ ...entry, bullets: [...entry.bullets] })),
    createStarterEntryForSection(section.sectionType, section.entries.length)
  ];

  normalizeEntrySortOrders(nextEntries);

  return {
    ...section,
    entries: nextEntries
  };
}

/** New typed Section with a Starter Entry (Contact uses labeled channel starters). */
export function createSectionOfType(
  sectionType: CvSectionType,
  sortOrder: number
): CvStructuredSection {
  const section = createEmptySection(sortOrder);

  return {
    ...section,
    heading: DEFAULT_SECTION_HEADINGS[sectionType] ?? sectionType,
    sectionType,
    entries:
      sectionType === 'Contact'
        ? createStarterContactEntries()
        : [createStarterEntryForSection(sectionType, 0)]
  };
}

export function appendSectionOfType(
  sections: readonly CvStructuredSection[],
  sectionType: CvSectionType
): CvStructuredSection[] {
  const next = [
    ...sections.map((section) => ({
      ...section,
      entries: section.entries.map((entry) => ({ ...entry, bullets: [...entry.bullets] }))
    })),
    createSectionOfType(sectionType, sections.length)
  ];

  normalizeSectionSortOrders(next);
  return next;
}
