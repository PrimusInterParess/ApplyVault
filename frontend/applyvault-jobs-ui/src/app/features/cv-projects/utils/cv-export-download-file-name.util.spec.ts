import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from './cv-structured-draft.util';
import {
  buildCvExportDownloadFileName,
  resolveCvExportPersonName,
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
  it('builds {Name}-CV.pdf download names', () => {
    expect(buildCvExportDownloadFileName('Jane Doe')).toBe('Jane-Doe-CV.pdf');
  });

  it('falls back to CV.pdf when person name is missing', () => {
    expect(buildCvExportDownloadFileName(null)).toBe('CV.pdf');
    expect(buildCvExportDownloadFileName(undefined)).toBe('CV.pdf');
    expect(buildCvExportDownloadFileName('   ')).toBe('CV.pdf');
  });

  it('strips unsafe filename characters', () => {
    expect(sanitizeCvExportFileNameSegment('Jane/Doe: Jr?*')).toBe('JaneDoe-Jr');
    expect(buildCvExportDownloadFileName('A/B <C>')).toBe('AB-C-CV.pdf');
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
});
