import { createEmptySection } from './cv-structured-draft.util';
import {
  addEntryLabelForSection,
  applyCvTemplateInlineEdit
} from './cv-template-inline-edit.util';
import { CV_FIXED_SECTIONS } from '../models/cv-fixed-sections.model';
import { CvStructuredSection } from '../models/cv-structured.model';

describe('cv-template-inline-edit.util', () => {
  function emptySlot(sectionType: CvStructuredSection['sectionType'], index: number): CvStructuredSection {
    return {
      ...createEmptySection(index),
      id: `${sectionType.toLowerCase()}-1`,
      heading: sectionType === 'Custom' ? 'Certifications & Languages' : sectionType,
      sectionType,
      entries: []
    };
  }

  it('labels Custom add-entry as Add certification or language', () => {
    expect(addEntryLabelForSection('Custom')).toBe('Add certification or language');
  });

  it('adds a starter entry to a zero-entry Experience section', () => {
    const sections = CV_FIXED_SECTIONS.map((slot, index) => emptySlot(slot.sectionType, index));
    const next = applyCvTemplateInlineEdit(sections, { kind: 'addEntry', sectionId: 'experience-1' });
    const experience = next.find((section) => section.sectionType === 'Experience');

    expect(experience?.entries).toHaveSize(1);
    expect(experience?.entries[0].bullets).toEqual(['']);
    expect(next).toHaveSize(7);
  });

  it('adds the first Summary entry when the slot is empty', () => {
    const sections = CV_FIXED_SECTIONS.map((slot, index) => emptySlot(slot.sectionType, index));
    const next = applyCvTemplateInlineEdit(sections, { kind: 'addEntry', sectionId: 'summary-1' });
    const summary = next.find((section) => section.sectionType === 'Summary');

    expect(summary?.entries).toHaveSize(1);
    expect(summary?.entries[0].summary).toBe('');
  });

  it('does not include a section when adding an entry', () => {
    const sections = CV_FIXED_SECTIONS.map((slot, index) => emptySlot(slot.sectionType, index)).map((section) =>
      section.sectionType === 'Projects' ? { ...section, includedOnTemplate: false } : section
    );
    const next = applyCvTemplateInlineEdit(sections, { kind: 'addEntry', sectionId: 'projects-1' });
    const projects = next.find((section) => section.sectionType === 'Projects');

    expect(projects?.entries).toHaveSize(1);
    expect(projects?.includedOnTemplate).toBeFalse();
  });

  it('sets includedOnTemplate from a sectionVisibility edit and keeps the section', () => {
    const sections = CV_FIXED_SECTIONS.map((slot, index) => emptySlot(slot.sectionType, index));
    const next = applyCvTemplateInlineEdit(sections, {
      kind: 'sectionVisibility',
      sectionId: 'summary-1',
      includedOnTemplate: false
    });
    const summary = next.find((section) => section.id === 'summary-1');

    expect(summary?.sectionType).toBe('Summary');
    expect(summary?.includedOnTemplate).toBeFalse();
    expect(next).toHaveSize(7);
  });
});
