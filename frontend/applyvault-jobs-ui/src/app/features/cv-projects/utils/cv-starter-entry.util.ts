import { CvSectionType, CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import {
  createContactFieldEntry,
  createStarterContactEntries
} from './cv-contact-channels.util';
import {
  createEmptyEntry,
  normalizeEntrySortOrders
} from './cv-structured-draft.util';

export { createStarterContactEntries };

const BULLET_SLOT_SECTION_TYPES = new Set<CvSectionType>([
  'Experience',
  'Education',
  'Projects'
]);

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
