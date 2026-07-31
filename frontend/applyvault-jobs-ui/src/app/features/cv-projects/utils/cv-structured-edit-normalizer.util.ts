import { CvSectionType, CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { dedupeContactEntries, ensureModernContactShape } from './cv-contact-channels.util';

export function normalizeSectionForEditing(section: CvStructuredSection): CvStructuredSection {
  const isContact =
    section.sectionType === 'Contact' || section.heading.trim().toLowerCase() === 'contact';

  const entries = section.entries.map((entry) =>
    normalizeEntryForEditing(entry, section.sectionType, isContact)
  );

  return {
    ...section,
    entries: isContact
      ? dedupeContactEntries(entries).map((entry, index) => ({ ...entry, sortOrder: index }))
      : entries
  };
}

/**
 * Clone + normalize every section for Content hydrate / open:
 * Summary title→summary, Skills bullets↔techStack, Contact summary→bullets,
 * then Contact modern expand (import-legacy + valued multi-bullet channels).
 */
export function normalizeSectionsForEditing(
  sections: readonly CvStructuredSection[]
): CvStructuredSection[] {
  return sections.map((section) => {
    const cloned: CvStructuredSection = {
      ...section,
      entries: section.entries.map((entry) => ({
        ...entry,
        bullets: [...entry.bullets],
        fields: { ...(entry.fields ?? {}) }
      }))
    };

    const normalized = normalizeSectionForEditing(cloned);
    const isContact =
      normalized.sectionType === 'Contact' || normalized.heading.trim().toLowerCase() === 'contact';

    return isContact ? ensureModernContactShape(normalized) : normalized;
  });
}

export function normalizeEntryForEditing(
  entry: CvStructuredEntry,
  sectionType: CvSectionType,
  isContactSection = false
): CvStructuredEntry {
  const base: CvStructuredEntry = {
    ...entry,
    bullets: [...entry.bullets],
    fields: { ...(entry.fields ?? {}) }
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
    return bullets.length === 0 ? entry : { ...entry, bullets: [] };
  }

  if (bullets.length === 0) {
    return entry;
  }

  return {
    ...entry,
    techStack: bullets.join(', '),
    bullets: []
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
