import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from './cv-structured-draft.util';
import {
  buildCvExportDownloadFileName,
  resolveCvExportPersonName,
  resolveCvExportTemplateLabel,
  sanitizeCvExportFileNameSegment
} from './cv-export-download-file-name.util';

function contactSection(entries: CvStructuredEntry[]): CvStructuredSection {
  return {
    ...createEmptySection(0),
    sectionType: 'Contact',
    heading: 'Contact',
    entries
  };
}

describe('cv-export-download-file-name.util', () => {
  it('builds person + template download names', () => {
    expect(buildCvExportDownloadFileName('Jane Doe', 'Modern')).toBe('Jane-Doe-Modern.pdf');
    expect(buildCvExportDownloadFileName('Jane Doe', 'Minimal')).toBe('Jane-Doe-Minimal.pdf');
  });

  it('falls back to CV when person name is missing', () => {
    expect(buildCvExportDownloadFileName(null, 'Modern')).toBe('CV-Modern.pdf');
    expect(buildCvExportDownloadFileName('   ', 'Minimal')).toBe('CV-Minimal.pdf');
  });

  it('strips unsafe filename characters', () => {
    expect(sanitizeCvExportFileNameSegment('Jane/Doe: Jr?*')).toBe('JaneDoe-Jr');
    expect(buildCvExportDownloadFileName('A/B <C>', 'Modern')).toBe('AB-C-Modern.pdf');
  });

  it('resolves person name from Contact Name subtitle', () => {
    const sections = [
      contactSection([
        {
          ...createEmptyEntry(0),
          title: 'Name',
          subtitle: 'Alex Rivera'
        }
      ])
    ];

    expect(resolveCvExportPersonName(sections)).toBe('Alex Rivera');
    expect(resolveCvExportPersonName([])).toBeNull();
    expect(resolveCvExportPersonName(null)).toBeNull();
  });

  it('maps template ids to gallery labels', () => {
    expect(resolveCvExportTemplateLabel(1)).toBe('Modern');
    expect(resolveCvExportTemplateLabel(2)).toBe('Modern');
    expect(resolveCvExportTemplateLabel(3)).toBe('Minimal');
    expect(resolveCvExportTemplateLabel(99)).toBe('Modern');
  });
});
