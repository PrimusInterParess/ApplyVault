import { CvStructuredDocument, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from './cv-structured-draft.util';
import { mergeAssistStructuredUpdate } from './cv-structured-assist-merge.util';

describe('mergeAssistStructuredUpdate section visibility', () => {
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

  it('stamps the previous includedOnTemplate before normalize', () => {
    const previous = doc([
      {
        ...section('summary-1', 'Summary', 'Summary', 1, 'Old summary'),
        includedOnTemplate: false
      }
    ]);
    const aiResult = doc([
      {
        ...section('summary-1', 'Summary', 'Summary', 1, 'New AI summary'),
        includedOnTemplate: true
      }
    ]);

    const merged = mergeAssistStructuredUpdate(previous, aiResult, ['summary-1']);
    const summary = merged.sections.find((item) => item.id === 'summary-1');

    expect(summary?.entries[0].summary).toBe('New AI summary');
    expect(summary?.includedOnTemplate).toBeFalse();
  });
});
