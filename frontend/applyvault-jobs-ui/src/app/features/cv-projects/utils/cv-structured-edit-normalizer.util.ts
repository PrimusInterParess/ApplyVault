import { CvSectionType, CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';

export function normalizeSectionForEditing(section: CvStructuredSection): CvStructuredSection {
  const isContact = section.heading.trim().toLowerCase() === 'contact';

  return {
    ...section,
    entries: section.entries.map((entry) =>
      normalizeEntryForEditing(entry, section.sectionType, isContact)
    )
  };
}

export function normalizeEntryForEditing(
  entry: CvStructuredEntry,
  sectionType: CvSectionType,
  isContactSection = false
): CvStructuredEntry {
  const base: CvStructuredEntry = {
    ...entry,
    bullets: [...entry.bullets]
  };

  if (isContactSection) {
    return normalizeContactEntry(base);
  }

  switch (sectionType) {
    case 'Skills':
      return normalizeSkillsEntry(base);
    case 'Summary':
      return normalizeSummaryEntry(base);
    default:
      return base;
  }
}

function normalizeContactEntry(entry: CvStructuredEntry): CvStructuredEntry {
  const bullets = entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0);
  const summary = entry.summary.trim();

  if (bullets.length > 0 || !summary) {
    return entry;
  }

  return {
    ...entry,
    bullets: summary
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0)
  };
}

function normalizeSkillsEntry(entry: CvStructuredEntry): CvStructuredEntry {
  const techStack = entry.techStack.trim();
  const bullets = entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0);

  if (techStack) {
    return entry;
  }

  if (bullets.length === 0) {
    return entry;
  }

  return {
    ...entry,
    techStack: bullets.join(', ')
  };
}

function normalizeSummaryEntry(entry: CvStructuredEntry): CvStructuredEntry {
  const summary = entry.summary.trim();
  const title = entry.title.trim();

  if (summary || !title) {
    return entry;
  }

  return {
    ...entry,
    summary: title
  };
}

/** Text shown in read-mode entry body; use the same source when binding edit fields. */
export function entryBodySourceText(entry: CvStructuredEntry, sectionType: CvSectionType): string {
  const summary = entry.summary.trim();

  if (summary) {
    return entry.summary;
  }

  if (sectionType === 'Summary' && entry.title.trim()) {
    return entry.title;
  }

  return entry.summary;
}
