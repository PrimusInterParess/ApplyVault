import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, normalizeEntrySortOrders } from './cv-structured-draft.util';

const ENTRY_SEPARATOR = '\n\n---\n\n';

export function sectionToMarkdown(section: CvStructuredSection): string {
  const heading = section.heading.trim() || 'Untitled section';
  const entryBlocks = [...section.entries]
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((entry) => entryToMarkdown(entry))
    .filter((block) => block.length > 0);

  if (entryBlocks.length === 0) {
    return `# ${heading}`;
  }

  return `# ${heading}\n\n${entryBlocks.join(ENTRY_SEPARATOR)}`;
}

export function parseSectionMarkdown(
  text: string,
  section: CvStructuredSection
): CvStructuredSection {
  const trimmed = text.trim();

  if (!trimmed) {
    return {
      ...section,
      heading: 'Untitled section',
      entries: []
    };
  }

  let body = trimmed;
  let heading = section.heading;

  if (body.startsWith('# ')) {
    const firstBreak = body.indexOf('\n');
    heading = (firstBreak === -1 ? body.slice(2) : body.slice(2, firstBreak)).trim();
    body = firstBreak === -1 ? '' : body.slice(firstBreak + 1).trim();
  }

  const entryChunks = body
    ? body.split(/\n---\n/).map((chunk) => chunk.trim()).filter(Boolean)
    : [];

  const sortedOriginal = [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);

  const entries = entryChunks.map((chunk, index) =>
    parseEntryMarkdown(chunk, sortedOriginal[index] ?? createEmptyEntry(index))
  );

  normalizeEntrySortOrders(entries);

  return {
    ...section,
    heading: heading || 'Untitled section',
    entries
  };
}

function entryToMarkdown(entry: CvStructuredEntry): string {
  const lines: string[] = [];

  if (entry.title.trim()) {
    lines.push(`## ${entry.title.trim()}`);
  }

  const meta = [entry.subtitle?.trim(), entry.dateRange?.trim()].filter(Boolean).join(' · ');

  if (meta) {
    lines.push(meta);
  }

  if (entry.summary.trim()) {
    if (lines.length > 0) {
      lines.push('');
    }

    lines.push(entry.summary.trim());
  }

  if (entry.bullets.length > 0) {
    if (lines.length > 0) {
      lines.push('');
    }

    lines.push(...entry.bullets.map((bullet) => `- ${bullet.trim()}`).filter((line) => line.length > 2));
  }

  if (entry.techStack.trim()) {
    if (lines.length > 0) {
      lines.push('');
    }

    lines.push(entry.techStack.trim());
  }

  return lines.join('\n');
}

function parseEntryMarkdown(chunk: string, template: CvStructuredEntry): CvStructuredEntry {
  const lines = chunk.split('\n');
  let index = 0;

  let title = template.title;

  if (lines[index]?.startsWith('## ')) {
    title = lines[index].slice(3).trim();
    index += 1;
  } else if (lines[index]?.trim()) {
    title = lines[index].trim();
    index += 1;
  }

  let subtitle: string | null = null;
  let dateRange: string | null = null;

  if (index < lines.length && lines[index].trim() && !lines[index].startsWith('- ')) {
    const meta = lines[index].trim();

    if (meta.includes(' · ')) {
      const [sub, date] = meta.split(' · ').map((part) => part.trim());
      subtitle = sub || null;
      dateRange = date || null;
    } else {
      subtitle = meta;
    }

    index += 1;
  }

  const summaryLines: string[] = [];
  const bullets: string[] = [];
  const tailLines: string[] = [];
  let phase: 'summary' | 'bullets' | 'tail' = 'summary';

  for (; index < lines.length; index += 1) {
    const line = lines[index];

    if (line.startsWith('- ')) {
      phase = 'bullets';
      bullets.push(line.slice(2).trim());
      continue;
    }

    if (phase === 'bullets') {
      if (!line.trim()) {
        phase = 'tail';
        continue;
      }

      phase = 'tail';
      tailLines.push(line);
      continue;
    }

    if (phase === 'tail') {
      tailLines.push(line);
      continue;
    }

    summaryLines.push(line);
  }

  return {
    ...template,
    title,
    subtitle,
    dateRange,
    summary: summaryLines.join('\n').trim(),
    bullets,
    techStack: tailLines.join('\n').trim()
  };
}
