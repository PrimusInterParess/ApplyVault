import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { normalizeToFixedSections } from '../models/cv-fixed-sections.model';
import {
  createEmptyEntry,
  createEmptySection,
  sectionsAreEqual,
  toSaveRequest
} from './cv-structured-draft.util';

describe('cv-structured-draft.util sectionsAreEqual', () => {
  function skillsSection(entry: Partial<CvStructuredEntry>): CvStructuredSection {
    return {
      ...createEmptySection(0),
      id: 'skills-1',
      sectionType: 'Skills',
      heading: 'Skills',
      entries: [
        {
          ...createEmptyEntry(0),
          id: 'entry-1',
          ...entry
        }
      ]
    };
  }

  it('treats Skills bullets and techStack as equivalent after normalize', () => {
    const withBullets = [
      skillsSection({ bullets: ['TypeScript', 'Angular'], techStack: '' })
    ];
    const withTechStack = [
      skillsSection({ bullets: [], techStack: 'TypeScript, Angular' })
    ];

    expect(sectionsAreEqual(withBullets, withTechStack)).toBeTrue();
  });

  it('treats Contact summary-only and bullets as equivalent after normalize', () => {
    const withSummary: CvStructuredSection[] = [
      {
        ...createEmptySection(0),
        id: 'contact-1',
        sectionType: 'Contact',
        heading: 'Contact',
        entries: [
          {
            ...createEmptyEntry(0),
            id: 'email-1',
            title: 'Email',
            summary: 'a@b.com',
            bullets: []
          }
        ]
      }
    ];
    const withBullets: CvStructuredSection[] = [
      {
        ...createEmptySection(0),
        id: 'contact-1',
        sectionType: 'Contact',
        heading: 'Contact',
        entries: [
          {
            ...createEmptyEntry(0),
            id: 'email-1',
            title: 'Email',
            summary: '',
            bullets: ['a@b.com']
          }
        ]
      }
    ];

    expect(sectionsAreEqual(withSummary, withBullets)).toBeTrue();
  });

  it('trims optional null/empty subtitle differences', () => {
    const left = [
      {
        ...createEmptySection(0),
        id: 's1',
        entries: [{ ...createEmptyEntry(0), id: 'e1', title: 'Role', subtitle: null }]
      }
    ];
    const right = [
      {
        ...createEmptySection(0),
        id: 's1',
        entries: [{ ...createEmptyEntry(0), id: 'e1', title: 'Role', subtitle: '   ' }]
      }
    ];

    expect(sectionsAreEqual(left, right)).toBeTrue();
  });

  it('keeps includedOnTemplate false on the save payload and in equality', () => {
    const omitted = normalizeToFixedSections([
      {
        ...createEmptySection(1),
        id: 'summary-1',
        sectionType: 'Summary',
        heading: 'Summary',
        includedOnTemplate: false
      }
    ]);
    const summary = toSaveRequest(omitted).sections.find((section) => section.sectionType === 'Summary');
    const contact = toSaveRequest(omitted).sections.find((section) => section.sectionType === 'Contact');

    expect(summary?.includedOnTemplate).toBeFalse();
    expect(contact?.includedOnTemplate).toBeTrue();

    const included = omitted.map((section) =>
      section.sectionType === 'Summary' ? { ...section, includedOnTemplate: true } : section
    );

    expect(sectionsAreEqual(omitted, included)).toBeFalse();
    expect(sectionsAreEqual(omitted, omitted.map((section) => ({ ...section })))).toBeTrue();
  });
});
