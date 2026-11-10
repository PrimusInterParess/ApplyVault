import { CvStructuredEntry, CvStructuredSection } from './cv-structured.model';
import { CV_FIXED_SECTIONS, normalizeToFixedSections } from './cv-fixed-sections.model';

describe('normalizeToFixedSections', () => {
  function entry(id: string, title: string, sortOrder = 0): CvStructuredEntry {
    return {
      id,
      title,
      subtitle: null,
      dateRange: null,
      summary: '',
      bullets: [],
      techStack: '',
      fields: {},
      source: 'Manual',
      sourceSummaryId: null,
      sortOrder
    };
  }

  function section(
    partial: Partial<CvStructuredSection> & Pick<CvStructuredSection, 'id' | 'sectionType'>
  ): CvStructuredSection {
    return {
      heading: partial.heading ?? partial.sectionType,
      sortOrder: partial.sortOrder ?? 0,
      entries: partial.entries ?? [],
      ...partial
    };
  }

  it('declares the seven catalog slots in order', () => {
    expect(CV_FIXED_SECTIONS.map((slot) => [slot.sectionType, slot.heading])).toEqual([
      ['Contact', 'Contact'],
      ['Summary', 'Summary'],
      ['Skills', 'Skills'],
      ['Experience', 'Experience'],
      ['Projects', 'Projects'],
      ['Education', 'Education'],
      ['Custom', 'Certifications & Languages']
    ]);
  });

  it('creates missing slots and forces heading plus sort order', () => {
    const result = normalizeToFixedSections([
      section({
        id: 'exp-1',
        sectionType: 'Experience',
        heading: 'Work history',
        sortOrder: 4,
        entries: [entry('e1', 'Engineer')]
      })
    ]);

    expect(result).toHaveSize(7);
    expect(result.map((item) => item.sectionType)).toEqual(CV_FIXED_SECTIONS.map((slot) => slot.sectionType));
    expect(result.map((item) => item.heading)).toEqual(CV_FIXED_SECTIONS.map((slot) => slot.heading));
    expect(result.map((item) => item.sortOrder)).toEqual([0, 1, 2, 3, 4, 5, 6]);
    expect(result.find((item) => item.sectionType === 'Experience')?.id).toBe('exp-1');
    expect(result.find((item) => item.sectionType === 'Experience')?.entries[0].title).toBe('Engineer');
    expect(result.find((item) => item.sectionType === 'Summary')?.entries).toEqual([]);
  });

  it('maps heading Contact to the Contact slot even when typed Custom', () => {
    const result = normalizeToFixedSections([
      section({
        id: 'profile-1',
        sectionType: 'Custom',
        heading: 'contact',
        entries: [entry('n1', 'Name')]
      })
    ]);

    const contact = result.find((item) => item.sectionType === 'Contact');
    expect(contact?.id).toBe('profile-1');
    expect(contact?.entries[0].title).toBe('Name');
    expect(result.find((item) => item.sectionType === 'Custom')?.entries).toEqual([]);
  });

  it('merges duplicate-type sections by concatenating entries and keeping the first id', () => {
    const result = normalizeToFixedSections([
      section({
        id: 'exp-a',
        sectionType: 'Experience',
        sortOrder: 1,
        entries: [entry('a1', 'First', 0)]
      }),
      section({
        id: 'exp-b',
        sectionType: 'Experience',
        sortOrder: 2,
        entries: [entry('b1', 'Second', 0), entry('b2', 'Third', 1)]
      })
    ]);

    const experience = result.find((item) => item.sectionType === 'Experience');
    expect(experience?.id).toBe('exp-a');
    expect(experience?.entries.map((item) => item.title)).toEqual(['First', 'Second', 'Third']);
    expect(experience?.entries.map((item) => item.sortOrder)).toEqual([0, 1, 2]);
  });

  it('folds unknown and extra Custom sections into Certifications & Languages', () => {
    const result = normalizeToFixedSections([
      section({
        id: 'awards-1',
        sectionType: 'Custom',
        heading: 'Awards',
        sortOrder: 0,
        entries: [entry('a1', 'Prize')]
      }),
      section({
        id: 'vol-1',
        sectionType: 'Custom',
        heading: 'Volunteer',
        sortOrder: 1,
        entries: [entry('v1', 'Mentor')]
      }),
      section({
        id: 'mystery-1',
        sectionType: 'Unknown' as CvStructuredSection['sectionType'],
        heading: 'Hobbies',
        sortOrder: 2,
        entries: [entry('h1', 'Chess')]
      })
    ]);

    const custom = result.find((item) => item.sectionType === 'Custom');
    expect(custom?.id).toBe('awards-1');
    expect(custom?.heading).toBe('Certifications & Languages');
    expect(custom?.entries.map((item) => item.title)).toEqual(['Prize', 'Mentor', 'Chess']);
  });

  it('copies includedOnTemplate false, forces Contact included, and includes a missing slot', () => {
    const result = normalizeToFixedSections([
      section({
        id: 'contact-1',
        sectionType: 'Contact',
        heading: 'Contact',
        includedOnTemplate: false,
        entries: [entry('n1', 'Name')]
      }),
      section({
        id: 'sum-1',
        sectionType: 'Summary',
        heading: 'Profile',
        sortOrder: 1,
        includedOnTemplate: false,
        entries: [entry('s1', 'Profile')]
      })
    ]);

    expect(result.find((item) => item.sectionType === 'Contact')?.includedOnTemplate).toBeTrue();
    expect(result.find((item) => item.sectionType === 'Summary')?.includedOnTemplate).toBeFalse();
    expect(result.find((item) => item.sectionType === 'Experience')?.includedOnTemplate).toBeTrue();
    expect(result.find((item) => item.sectionType === 'Summary')?.id).toBe('sum-1');
  });

  it('is idempotent for ids, headings, and merged entries', () => {
    const first = normalizeToFixedSections([
      section({
        id: 'sum-1',
        sectionType: 'Summary',
        heading: 'Profile',
        entries: [entry('s1', '', 0)]
      }),
      section({
        id: 'extra-1',
        sectionType: 'Custom',
        heading: 'Languages',
        entries: [entry('c1', 'French')]
      })
    ]);
    const second = normalizeToFixedSections(first);

    expect(second.map((item) => item.id)).toEqual(first.map((item) => item.id));
    expect(second.map((item) => item.heading)).toEqual(first.map((item) => item.heading));
    expect(second[6].entries.map((item) => item.title)).toEqual(['French']);
  });
});
