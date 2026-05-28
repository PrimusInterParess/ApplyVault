import { normalizeEntryForEditing, normalizeSectionForEditing } from './cv-structured-edit-normalizer.util';
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
});
