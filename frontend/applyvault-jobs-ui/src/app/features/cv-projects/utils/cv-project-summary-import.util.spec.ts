import { normalizeToFixedSections } from '../models/cv-fixed-sections.model';
import { CvProjectSummary } from '../models/cv-project.model';
import { appendProjectSummariesToSections } from './cv-project-summary-import.util';

describe('appendProjectSummariesToSections', () => {
  const summary: CvProjectSummary = {
    id: 'repo-summary-1',
    externalRepoId: 42,
    fullName: 'acme/widget',
    htmlUrl: 'https://example.test/acme/widget',
    primaryLanguage: 'TypeScript',
    topics: [],
    cvTitle: 'Widget',
    cvSummary: 'Built a widget',
    cvBullets: ['Shipped it'],
    techStack: 'Angular',
    generatedAt: '2026-10-19T00:00:00Z',
    updatedAt: '2026-10-19T00:00:00Z'
  };

  it('leaves includedOnTemplate false when appending a project summary', () => {
    const sections = normalizeToFixedSections([
      {
        id: 'projects-1',
        heading: 'Projects',
        sectionType: 'Projects',
        sortOrder: 4,
        includedOnTemplate: false,
        entries: []
      }
    ]);

    const projects = appendProjectSummariesToSections(sections, [summary]);

    expect(projects.includedOnTemplate).toBeFalse();
    expect(projects.entries).toHaveSize(1);
    expect(projects.entries[0].title).toBe('Widget');
    expect(sections.find((section) => section.sectionType === 'Projects')?.includedOnTemplate).toBeFalse();
  });
});
