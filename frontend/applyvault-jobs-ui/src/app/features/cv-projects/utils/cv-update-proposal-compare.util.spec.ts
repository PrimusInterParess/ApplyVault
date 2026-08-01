import { CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from './cv-structured-draft.util';
import {
  formatSectionForAssistCompare,
  resolveUpdateProposalCompareSectionIds
} from './cv-update-proposal-compare.util';

describe('cv-update-proposal-compare.util', () => {
  it('formats section entries as readable Current/Proposed text', () => {
    const section: CvStructuredSection = {
      ...createEmptySection(0),
      heading: 'Experience',
      sectionType: 'Experience',
      entries: [
        {
          ...createEmptyEntry(0),
          title: 'Engineer',
          subtitle: 'Acme',
          dateRange: '2020–2024',
          summary: 'Built APIs',
          bullets: ['Shipped billing', 'Cut latency']
        }
      ]
    };

    const text = formatSectionForAssistCompare(section);

    expect(text).toContain('Experience');
    expect(text).toContain('Engineer');
    expect(text).toContain('Acme');
    expect(text).toContain('• Shipped billing');
  });

  it('uses focus ids when present for compare panes', () => {
    expect(
      resolveUpdateProposalCompareSectionIds(
        ['summary-1'],
        [{ ...createEmptySection(0), id: 'exp-1' }]
      )
    ).toEqual(['summary-1']);
  });

  it('falls back to proposed section ids when focus is empty', () => {
    expect(
      resolveUpdateProposalCompareSectionIds(
        [],
        [
          { ...createEmptySection(0), id: 'a' },
          { ...createEmptySection(1), id: 'b' }
        ]
      )
    ).toEqual(['a', 'b']);
  });
});
