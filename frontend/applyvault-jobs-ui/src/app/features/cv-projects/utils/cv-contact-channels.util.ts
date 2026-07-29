import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry } from './cv-structured-draft.util';

export function isContactNameEntry(entry: CvStructuredEntry): boolean {
  return entry.title.trim().toLowerCase() === 'name';
}

export function contactEntryValue(entry: CvStructuredEntry): string {
  const fromBullet = entry.bullets.find((line) => line.trim().length > 0)?.trim();

  if (fromBullet) {
    return fromBullet;
  }

  return entry.summary.trim();
}

export function contactDisplayLine(entry: CvStructuredEntry): string {
  if (isContactNameEntry(entry)) {
    return '';
  }

  return contactEntryValue(entry);
}

export function contactFieldEntries(section: CvStructuredSection): readonly CvStructuredEntry[] {
  return [...section.entries]
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .filter((entry) => !isContactNameEntry(entry));
}

/** @deprecated Prefer contactFieldEntries */
export function contactChannelEntries(section: CvStructuredSection): readonly CvStructuredEntry[] {
  return contactFieldEntries(section);
}

export function findContactNameEntry(section: CvStructuredSection): CvStructuredEntry | null {
  const sorted = [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);
  return sorted.find((entry) => isContactNameEntry(entry)) ?? null;
}

export function createContactNameEntry(sortOrder = 0): CvStructuredEntry {
  return {
    ...createEmptyEntry(sortOrder),
    title: 'Name'
  };
}

export function createContactFieldEntry(sortOrder: number, label = ''): CvStructuredEntry {
  return {
    ...createEmptyEntry(sortOrder),
    title: label.trim(),
    bullets: ['']
  };
}

/** @deprecated Prefer createContactFieldEntry */
export function createContactChannelEntry(label: string, sortOrder: number): CvStructuredEntry {
  return createContactFieldEntry(sortOrder, label);
}

export function createStarterContactEntries(): CvStructuredEntry[] {
  return [createContactNameEntry(0), createContactFieldEntry(1)];
}

/** One entry with multiple contact bullets — import / pre-multi-entry shape. */
export function isLegacyContactSection(section: CvStructuredSection): boolean {
  if (section.entries.length !== 1) {
    return false;
  }

  const only = section.entries[0];
  const title = only.title.trim().toLowerCase();
  const isNameOrBlank = title.length === 0 || title === 'name';

  return isNameOrBlank && only.bullets.filter((line) => line.trim().length > 0).length > 1;
}

/**
 * Expands a legacy Contact entry (many bullets) into Name + one field per line.
 * Mutates `section.entries` in place; call on a draft clone.
 */
export function ensureModernContactShape(section: CvStructuredSection): CvStructuredSection {
  const sorted = [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);

  if (sorted.length === 0) {
    section.entries = [createContactNameEntry(0), createContactFieldEntry(1)];
    return section;
  }

  if (isLegacyContactSection(section)) {
    const only = sorted[0];
    const lines = only.bullets.map((line) => line.trim()).filter((line) => line.length > 0);

    let nameSubtitle = only.subtitle?.trim() || null;

    if (!nameSubtitle && only.title.trim() && only.title.trim().toLowerCase() !== 'name') {
      nameSubtitle = only.title.trim();
    }

    section.entries = [
      {
        ...createContactNameEntry(0),
        id: only.id,
        subtitle: nameSubtitle
      },
      ...lines.map((line, index) => ({
        ...createContactFieldEntry(index + 1),
        id: `${only.id}__ch${index}`,
        bullets: [line]
      }))
    ];
    return section;
  }

  if (!findContactNameEntry(section)) {
    section.entries = [createContactNameEntry(0), ...section.entries];
  }

  return section;
}

/** Non-mutating view of Contact entries in the modern Name + fields shape. */
export function contactSectionForDisplay(section: CvStructuredSection): CvStructuredSection {
  if (!isLegacyContactSection(section)) {
    return section;
  }

  const draft: CvStructuredSection = {
    ...section,
    entries: section.entries.map((entry) => ({
      ...entry,
      bullets: [...entry.bullets],
      fields: { ...entry.fields }
    }))
  };

  return ensureModernContactShape(draft);
}
