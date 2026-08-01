import { CvStructuredDocument, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from './cv-structured-draft.util';
import {
  assistMergeRequiresPersist,
  mergeAssistStructuredUpdate
} from './cv-structured-assist-merge.util';

describe('mergeAssistStructuredUpdate', () => {
  function section(
    id: string,
    heading: string,
    sectionType: CvStructuredSection['sectionType'],
    sortOrder: number,
    marker: string
  ): CvStructuredSection {
    return {
      ...createEmptySection(sortOrder),
      id,
      heading,
      sectionType,
      entries: [
        {
          ...createEmptyEntry(0),
          id: `${id}-entry`,
          title: marker,
          summary: marker
        }
      ]
    };
  }

  function doc(sections: CvStructuredSection[]): CvStructuredDocument {
    return {
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections
    };
  }

  it('with focus ids: replaces only focused sections and preserves others', () => {
    const previous = doc([
      section('contact-1', 'Contact', 'Contact', 0, 'Alice'),
      section('summary-1', 'Summary', 'Summary', 1, 'Old summary'),
      section('exp-1', 'Experience', 'Experience', 2, 'Acme'),
      section('edu-1', 'Education', 'Education', 3, 'Uni')
    ]);

    const aiResult = doc([
      section('contact-1', 'Contact', 'Contact', 0, ''),
      section('summary-1', 'Summary', 'Summary', 1, 'New AI summary')
    ]);

    const merged = mergeAssistStructuredUpdate(previous, aiResult, ['summary-1']);

    expect(merged.sections.map((item) => item.id)).toEqual([
      'contact-1',
      'summary-1',
      'exp-1',
      'edu-1'
    ]);
    expect(merged.sections.find((item) => item.id === 'summary-1')?.entries[0].summary).toBe(
      'New AI summary'
    );
    expect(merged.sections.find((item) => item.id === 'contact-1')?.entries[0].title).toBe('Alice');
    expect(merged.sections.find((item) => item.id === 'exp-1')?.entries[0].title).toBe('Acme');
    expect(merged.sections.find((item) => item.id === 'edu-1')?.entries[0].title).toBe('Uni');
    expect(assistMergeRequiresPersist(aiResult, merged)).toBeTrue();
  });

  it('with focus ids: ignores non-focused AI sections (Contact wipe defense)', () => {
    const previous = doc([
      section('contact-1', 'Contact', 'Contact', 0, 'Bob'),
      section('summary-1', 'Summary', 'Summary', 1, 'Old')
    ]);
    const aiResult = doc([
      section('contact-1', 'Contact', 'Contact', 0, ''),
      section('summary-1', 'Summary', 'Summary', 1, 'Updated')
    ]);

    const merged = mergeAssistStructuredUpdate(previous, aiResult, ['summary-1']);

    expect(merged.sections.find((item) => item.id === 'contact-1')?.entries[0].title).toBe('Bob');
    expect(merged.sections.find((item) => item.id === 'summary-1')?.entries[0].summary).toBe(
      'Updated'
    );
  });

  it('without focus: preserves local sections omitted from a partial AI payload', () => {
    const previous = doc([
      section('summary-1', 'Summary', 'Summary', 0, 'S'),
      section('exp-1', 'Experience', 'Experience', 1, 'Acme'),
      section('edu-1', 'Education', 'Education', 2, 'Uni')
    ]);
    const aiResult = doc([section('summary-1', 'Summary', 'Summary', 0, 'Rewritten')]);

    const merged = mergeAssistStructuredUpdate(previous, aiResult);

    expect(merged.sections.map((item) => item.id)).toEqual(['summary-1', 'exp-1', 'edu-1']);
    expect(merged.sections[0].entries[0].summary).toBe('Rewritten');
    expect(assistMergeRequiresPersist(aiResult, merged)).toBeTrue();
  });

  it('without focus: appends AI-only new sections', () => {
    const previous = doc([section('summary-1', 'Summary', 'Summary', 0, 'S')]);
    const aiResult = doc([
      section('summary-1', 'Summary', 'Summary', 0, 'S2'),
      section('skills-1', 'Skills', 'Skills', 1, 'TS')
    ]);

    const merged = mergeAssistStructuredUpdate(previous, aiResult);

    expect(merged.sections.map((item) => item.id)).toEqual(['summary-1', 'skills-1']);
    expect(assistMergeRequiresPersist(aiResult, merged)).toBeFalse();
  });

  it('returns AI result unchanged when there is no previous document', () => {
    const aiResult = doc([section('summary-1', 'Summary', 'Summary', 0, 'Only')]);

    expect(mergeAssistStructuredUpdate(null, aiResult, ['summary-1'])).toBe(aiResult);
  });
});
