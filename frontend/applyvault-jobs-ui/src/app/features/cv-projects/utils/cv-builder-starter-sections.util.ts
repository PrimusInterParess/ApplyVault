import { CvSectionType, CvStructuredSection } from '../models/cv-structured.model';
import { createStarterContactEntries, createStarterEntryForSection } from './cv-starter-entry.util';
import { createEmptySection, normalizeSectionSortOrders } from './cv-structured-draft.util';

const STARTER_SECTION_TYPES: readonly CvSectionType[] = [
  'Contact',
  'Summary',
  'Experience',
  'Education',
  'Skills'
];

const DEFAULT_HEADINGS: Record<string, string> = {
  Contact: 'Contact',
  Summary: 'Summary',
  Experience: 'Experience',
  Education: 'Education',
  Skills: 'Skills'
};

export function createBuilderStarterSections(): CvStructuredSection[] {
  const sections = STARTER_SECTION_TYPES.map((sectionType, index) => {
    const section = createEmptySection(index);

    return {
      ...section,
      heading: DEFAULT_HEADINGS[sectionType],
      sectionType,
      entries:
        sectionType === 'Contact'
          ? createStarterContactEntries()
          : [createStarterEntryForSection(sectionType, 0)]
    };
  });

  normalizeSectionSortOrders(sections);
  return sections;
}
