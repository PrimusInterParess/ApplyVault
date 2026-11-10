import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { partitionSectionsForExport } from './cv-export-template-layout.util';

describe('partitionSectionsForExport section visibility', () => {
  function entry(id: string, title: string): CvStructuredEntry {
    return {
      id,
      title,
      subtitle: title === 'Name' ? 'Ada' : null,
      dateRange: null,
      summary: title === 'Name' ? '' : 'Content',
      bullets: [],
      techStack: '',
      fields: {},
      source: 'Manual',
      sourceSummaryId: null,
      sortOrder: 0
    };
  }

  function section(
    sectionType: CvStructuredSection['sectionType'],
    includedOnTemplate: boolean
  ): CvStructuredSection {
    return {
      id: sectionType.toLowerCase(),
      heading: sectionType,
      sectionType,
      sortOrder: sectionType === 'Contact' ? 0 : sectionType === 'Summary' ? 1 : 3,
      includedOnTemplate,
      entries: [entry(`${sectionType}-entry`, sectionType === 'Contact' ? 'Name' : 'Role')]
    };
  }

  const sections = [
    section('Contact', false),
    section('Summary', false),
    section('Experience', true)
  ];

  it('drops an omitted non-Contact section from the edit canvas and the export', () => {
    const exported = partitionSectionsForExport(sections);
    const canvas = partitionSectionsForExport(sections, { includeEmpty: true, editCanvas: true });

    expect(exported.header.map((item) => item.sectionType)).toEqual(['Contact']);
    expect(exported.main.map((item) => item.sectionType)).toEqual(['Experience']);
    expect(canvas.header.map((item) => item.sectionType)).toEqual(['Contact']);
    expect(canvas.main.map((item) => item.sectionType)).toEqual(['Experience']);
  });

  it('places an included Summary in the main column with the other sections', () => {
    const included = [
      section('Contact', true),
      section('Summary', true),
      section('Experience', true)
    ];

    const exported = partitionSectionsForExport(included);

    expect(exported.header.map((item) => item.sectionType)).toEqual(['Contact']);
    expect(exported.main.map((item) => item.sectionType)).toEqual(['Summary', 'Experience']);
  });
});
