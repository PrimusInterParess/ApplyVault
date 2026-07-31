import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import {
  createEmptyEntry,
  createEmptySection,
  sectionsAreEqual
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
});
