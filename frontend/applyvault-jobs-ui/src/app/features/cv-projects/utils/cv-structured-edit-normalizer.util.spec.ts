import {
  normalizeEntryForEditing,
  normalizeSectionForEditing,
  normalizeSectionsForEditing
} from './cv-structured-edit-normalizer.util';
import { CvStructuredEntry } from '../models/cv-structured.model';

describe('cv-structured-edit-normalizer.util', () => {
  const baseEntry: CvStructuredEntry = {
    id: '1',
    title: '',
    subtitle: null,
    dateRange: null,
    summary: '',
    bullets: [],
    techStack: '',
    fields: {},
    source: 'Manual',
    sourceSummaryId: null,
    sortOrder: 0
  };

  it('copies skill bullets into techStack for editing', () => {
    const entry = normalizeEntryForEditing(
      { ...baseEntry, bullets: ['React', 'TypeScript'] },
      'Skills'
    );

    expect(entry.techStack).toBe('React, TypeScript');
  });

  it('copies summary title into summary text when summary is empty', () => {
    const entry = normalizeEntryForEditing(
      {
        ...baseEntry,
        title: 'Profile summary',
        bullets: ['Detail one', 'Detail two']
      },
      'Summary'
    );

    expect(entry.summary).toBe('Profile summary');
    expect(entry.bullets).toEqual(['Detail one', 'Detail two']);
  });

  it('preserves summary markdown for experience entries when editing', () => {
    const summary = 'Led delivery\n- Built APIs\n- Improved latency';
    const entry = normalizeEntryForEditing(
      {
        ...baseEntry,
        summary
      },
      'Experience'
    );

    expect(entry.summary).toBe(summary);
    expect(entry.bullets).toEqual([]);
  });

  it('copies contact summary lines into editable contact bullets', () => {
    const section = normalizeSectionForEditing({
      id: 'section-1',
      heading: 'Contact',
      sectionType: 'Custom',
      sortOrder: 0,
      entries: [{ ...baseEntry, summary: 'email@example.com\n+1 555 0100' }]
    });

    expect(section.entries[0]?.bullets).toEqual(['email@example.com', '+1 555 0100']);
  });

  it('preserves numbered markdown in summary for experience entries when editing', () => {
    const summary = 'Overview\n1. Built APIs\n2. Improved latency';
    const entry = normalizeEntryForEditing(
      {
        ...baseEntry,
        summary
      },
      'Experience'
    );

    expect(entry.summary).toBe(summary);
  });

  it('normalizes all entries in a section', () => {
    const section = normalizeSectionForEditing({
      id: 'section-1',
      heading: 'Skills',
      sectionType: 'Skills',
      sortOrder: 0,
      entries: [{ ...baseEntry, bullets: ['C#', 'SQL'] }]
    });

    expect(section.entries[0]?.techStack).toBe('C#, SQL');
  });

  it('dedupes Contact Phone field entries with the same value', () => {
    const section = normalizeSectionForEditing({
      id: 'section-1',
      heading: 'Contact',
      sectionType: 'Contact',
      sortOrder: 0,
      entries: [
        { ...baseEntry, id: 'n', title: 'Name', subtitle: 'Alex' },
        { ...baseEntry, id: 'p1', title: 'Phone', bullets: ['+45 12 34 56 78'], sortOrder: 1 },
        { ...baseEntry, id: 'p2', title: 'Phone', bullets: ['+45 12 34 56 78'], sortOrder: 2 }
      ]
    });

    const phones = section.entries.filter((entry) => entry.title.trim().toLowerCase() === 'phone');
    expect(phones.length).toBe(1);
    expect(phones[0]?.bullets[0]).toBe('+45 12 34 56 78');
  });

  it('normalizeSectionsForEditing applies Summary title→summary and Skills bullets→techStack', () => {
    const [summary, skills] = normalizeSectionsForEditing([
      {
        id: 'summary-1',
        heading: 'Summary',
        sectionType: 'Summary',
        sortOrder: 0,
        entries: [{ ...baseEntry, id: 's1', title: 'Imported blurb', summary: '' }]
      },
      {
        id: 'skills-1',
        heading: 'Skills',
        sectionType: 'Skills',
        sortOrder: 1,
        entries: [{ ...baseEntry, id: 'k1', bullets: ['Angular', 'RxJS'], techStack: '' }]
      }
    ]);

    expect(summary.entries[0]?.summary).toBe('Imported blurb');
    expect(skills.entries[0]?.techStack).toBe('Angular, RxJS');
    expect(skills.entries[0]?.bullets).toEqual([]);
  });

  it('normalizeSectionsForEditing expands Contact valued multi-bullet channels', () => {
    const [contact] = normalizeSectionsForEditing([
      {
        id: 'contact-1',
        heading: 'Contact',
        sectionType: 'Contact',
        sortOrder: 0,
        entries: [
          { ...baseEntry, id: 'n', title: 'Name', subtitle: 'Alex' },
          { ...baseEntry, id: 'e', title: 'Email', bullets: ['a@b.com', 'b@b.com'], sortOrder: 1 }
        ]
      }
    ]);

    expect(contact.entries.map((entry) => entry.bullets[0]).filter(Boolean)).toEqual([
      'a@b.com',
      'b@b.com'
    ]);
  });

  it('normalizeSectionsForEditing merges Name-bullet channels into starter Email/Phone/LinkedIn', () => {
    const [contact] = normalizeSectionsForEditing([
      {
        id: 'contact-1',
        heading: 'Contact',
        sectionType: 'Contact',
        sortOrder: 0,
        entries: [
          {
            ...baseEntry,
            id: 'n',
            title: 'Name',
            subtitle: 'Alex',
            bullets: ['a@b.com', '+45 12 34 56 78', 'linkedin.com/in/alex']
          },
          { ...baseEntry, id: 'e', title: 'Email', bullets: [''], sortOrder: 1 },
          { ...baseEntry, id: 'p', title: 'Phone', bullets: [''], sortOrder: 2 },
          { ...baseEntry, id: 'l', title: 'LinkedIn', bullets: [''], sortOrder: 3 }
        ]
      }
    ]);

    const fields = contact.entries.filter((entry) => entry.title.trim().toLowerCase() !== 'name');
    expect(fields.map((entry) => entry.title.trim().toLowerCase())).toEqual([
      'email',
      'phone',
      'linkedin'
    ]);
    expect(fields.map((entry) => entry.bullets[0])).toEqual([
      'a@b.com',
      '+45 12 34 56 78',
      'linkedin.com/in/alex'
    ]);
  });
});
