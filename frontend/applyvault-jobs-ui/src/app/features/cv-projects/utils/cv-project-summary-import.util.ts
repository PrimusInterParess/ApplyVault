import { CvProjectSummary } from '../models/cv-project.model';
import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';

/** Collect `sourceSummaryId` values already present on any entry. */
export function collectImportedProjectSummaryIds(
  sections: readonly CvStructuredSection[]
): ReadonlySet<string> {
  const ids = new Set<string>();

  for (const section of sections) {
    for (const entry of section.entries) {
      if (entry.sourceSummaryId) {
        ids.add(entry.sourceSummaryId);
      }
    }
  }

  return ids;
}

/** Ensure a Projects section exists; mutates `sections` when appending a new one. */
export function ensureProjectsSection(sections: CvStructuredSection[]): CvStructuredSection {
  const existing = sections.find((section) => section.sectionType === 'Projects');

  if (existing) {
    return existing;
  }

  const section: CvStructuredSection = {
    id: crypto.randomUUID(),
    heading: 'Projects',
    sectionType: 'Projects',
    sortOrder: sections.length,
    entries: []
  };

  sections.push(section);
  return section;
}

/** Map a saved project summary onto a Projects section entry (my-cv parity). */
export function projectSummaryToEntry(summary: CvProjectSummary, sortOrder: number): CvStructuredEntry {
  return {
    id: crypto.randomUUID(),
    title: summary.cvTitle,
    subtitle: summary.fullName,
    dateRange: null,
    summary: summary.cvSummary,
    bullets: [...summary.cvBullets],
    techStack: summary.techStack,
    fields: {},
    source: 'Project summary',
    sourceSummaryId: summary.id,
    sortOrder
  };
}

/**
 * Append selected summaries as Projects entries (skips already-imported ids).
 * Mutates the Projects section on `sections`; returns that section.
 */
export function appendProjectSummariesToSections(
  sections: CvStructuredSection[],
  summaries: readonly CvProjectSummary[]
): CvStructuredSection {
  const importedIds = collectImportedProjectSummaryIds(sections);
  const toImport = summaries.filter((summary) => !importedIds.has(summary.id));
  const projectsSection = ensureProjectsSection(sections);

  if (toImport.length === 0) {
    return projectsSection;
  }

  const nextEntries = [
    ...projectsSection.entries.map((entry) => ({ ...entry, bullets: [...entry.bullets] })),
    ...toImport.map((summary, index) =>
      projectSummaryToEntry(summary, projectsSection.entries.length + index)
    )
  ];

  projectsSection.entries = nextEntries.map((entry, index) => ({
    ...entry,
    sortOrder: index
  }));

  return projectsSection;
}
