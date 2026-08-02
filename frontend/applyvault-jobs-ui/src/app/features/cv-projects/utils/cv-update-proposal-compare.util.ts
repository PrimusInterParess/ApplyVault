import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';

/**
 * Readable plain-text snapshot of a section for Assist Current vs Proposed panes.
 */
export function formatSectionForAssistCompare(section: CvStructuredSection | undefined): string {
  if (!section) {
    return 'Section not present.';
  }

  const lines: string[] = [];
  const heading = section.heading.trim() || section.sectionType;
  lines.push(heading);

  const entries = [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);
  if (entries.length === 0) {
    lines.push('(empty)');
    return lines.join('\n');
  }

  for (const entry of entries) {
    const entryLines = formatEntryLines(entry);
    if (entryLines.length > 0) {
      if (lines.length > 1) {
        lines.push('');
      }
      lines.push(...entryLines);
    }
  }

  return lines.join('\n').trim() || '(empty)';
}

function formatEntryLines(entry: CvStructuredEntry): string[] {
  const lines: string[] = [];
  const title = entry.title?.trim() ?? '';
  const subtitle = entry.subtitle?.trim() ?? '';
  const dateRange = entry.dateRange?.trim() ?? '';
  const summary = entry.summary?.trim() ?? '';
  const techStack = entry.techStack?.trim() ?? '';
  const bullets = (entry.bullets ?? []).map((bullet) => bullet.trim()).filter(Boolean);

  if (title) {
    lines.push(title);
  }
  if (subtitle) {
    lines.push(subtitle);
  }
  if (dateRange) {
    lines.push(dateRange);
  }
  if (summary) {
    lines.push(summary);
  }
  for (const bullet of bullets) {
    lines.push(`• ${bullet}`);
  }
  if (techStack) {
    lines.push(techStack);
  }

  return lines;
}

/**
 * Section ids to show in the Update proposal review:
 * focus ids when present; otherwise only sections whose Current compare text
 * differs from Proposed (ADR-0011 / D3 — affected sections only).
 */
export function resolveUpdateProposalCompareSectionIds(
  focusSectionIds: readonly string[],
  proposedSections: readonly CvStructuredSection[],
  currentSections: readonly CvStructuredSection[] = []
): string[] {
  if (focusSectionIds.length > 0) {
    return [...focusSectionIds];
  }

  const currentById = new Map(currentSections.map((section) => [section.id, section]));
  return proposedSections
    .filter((proposed) => {
      const current = currentById.get(proposed.id);
      return (
        formatSectionForAssistCompare(current) !== formatSectionForAssistCompare(proposed)
      );
    })
    .map((section) => section.id);
}
