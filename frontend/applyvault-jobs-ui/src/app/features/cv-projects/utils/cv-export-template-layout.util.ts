import { CvSectionType, CvStructuredSection } from '../models/cv-structured.model';
import {
  contactEntryValue,
  isContactNameEntry
} from './cv-contact-channels.util';

export type CvExportPreviewZone = 'header' | 'sidebar' | 'main';

export interface CvExportPreviewLayout {
  readonly header: readonly CvStructuredSection[];
  readonly sidebar: readonly CvStructuredSection[];
  readonly main: readonly CvStructuredSection[];
}

function isContactSection(section: CvStructuredSection): boolean {
  return section.heading.trim().toLowerCase() === 'contact' || section.sectionType === 'Contact';
}

function isSidebarSection(section: CvStructuredSection): boolean {
  return (
    section.sectionType === 'Skills' ||
    section.sectionType === 'Summary' ||
    isContactSection(section)
  );
}

function isHeaderSection(section: CvStructuredSection): boolean {
  return section.sectionType === 'Summary' || isContactSection(section);
}

function sortSections(sections: readonly CvStructuredSection[]): CvStructuredSection[] {
  return [...sections].sort((left, right) => left.sortOrder - right.sortOrder);
}

/** Contact export/preview: labels alone do not count as content. */
export function contactSectionHasRenderableContent(section: CvStructuredSection): boolean {
  return section.entries.some((entry) => {
    if (isContactNameEntry(entry)) {
      return (entry.subtitle?.trim().length ?? 0) > 0;
    }

    return contactEntryValue(entry).length > 0;
  });
}

function sectionHasRenderableContent(section: CvStructuredSection): boolean {
  if (isContactSection(section)) {
    return contactSectionHasRenderableContent(section);
  }

  return section.entries.some(
    (entry) =>
      entry.title.trim().length > 0 ||
      (entry.subtitle?.trim().length ?? 0) > 0 ||
      (entry.dateRange?.trim().length ?? 0) > 0 ||
      entry.summary.trim().length > 0 ||
      entry.bullets.some((bullet) => bullet.trim().length > 0) ||
      entry.techStack.trim().length > 0
  );
}

export function partitionSectionsForTemplate(
  templateId: number,
  sections: readonly CvStructuredSection[],
  options?: { includeEmpty?: boolean }
): CvExportPreviewLayout {
  const includeEmpty = options?.includeEmpty ?? false;
  const ordered = sortSections(sections).filter(
    (section) => includeEmpty || sectionHasRenderableContent(section) || section.entries.length > 0
  );

  // Minimal (3): Contact + Summary in header (match CvExportHtmlMapper).
  if (templateId === 3) {
    const header: CvStructuredSection[] = [];
    const main: CvStructuredSection[] = [];

    for (const section of ordered) {
      if (isHeaderSection(section)) {
        header.push(section);
      } else {
        main.push(section);
      }
    }

    return { header, sidebar: [], main };
  }

  const sidebar: CvStructuredSection[] = [];
  const main: CvStructuredSection[] = [];

  for (const section of ordered) {
    if (isSidebarSection(section)) {
      sidebar.push(section);
    } else {
      main.push(section);
    }
  }

  return { header: [], sidebar, main };
}

export function createSamplePreviewSections(): CvStructuredSection[] {
  return [
    {
      id: 'sample-contact',
      heading: 'Contact',
      sectionType: 'Contact',
      sortOrder: 0,
      entries: [
        {
          id: 'sample-contact-name',
          title: 'Name',
          subtitle: 'Alex Jensen',
          dateRange: null,
          summary: '',
          bullets: [],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        },
        {
          id: 'sample-contact-email',
          title: '',
          subtitle: null,
          dateRange: null,
          summary: '',
          bullets: ['alex@example.com'],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 1
        },
        {
          id: 'sample-contact-phone',
          title: '',
          subtitle: null,
          dateRange: null,
          summary: '',
          bullets: ['+45 12 34 56 78'],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 2
        },
        {
          id: 'sample-contact-linkedin',
          title: '',
          subtitle: null,
          dateRange: null,
          summary: '',
          bullets: ['linkedin.com/in/alex'],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 3
        }
      ]
    },
    {
      id: 'sample-summary',
      heading: 'Summary',
      sectionType: 'Summary',
      sortOrder: 1,
      entries: [
        {
          id: 'sample-summary-entry',
          title: '',
          subtitle: null,
          dateRange: null,
          summary: 'Backend engineer with 8+ years building reliable APIs and data pipelines.',
          bullets: [],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        }
      ]
    },
    {
      id: 'sample-experience',
      heading: 'Experience',
      sectionType: 'Experience',
      sortOrder: 2,
      entries: [
        {
          id: 'sample-experience-entry',
          title: 'Senior Engineer',
          subtitle: 'Acme Corp',
          dateRange: '2021 – Present',
          summary: '',
          bullets: ['Led migration to event-driven architecture', 'Reduced p95 latency by 40%'],
          techStack: 'C#, PostgreSQL, Kafka',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        }
      ]
    },
    {
      id: 'sample-skills',
      heading: 'Skills',
      sectionType: 'Skills',
      sortOrder: 3,
      entries: [
        {
          id: 'sample-skills-entry',
          title: 'Core',
          subtitle: null,
          dateRange: null,
          summary: '',
          bullets: ['TypeScript', 'C#', 'SQL', 'Azure'],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        }
      ]
    }
  ];
}
