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
  return dedupeContactEntries(
    [...section.entries]
      .sort((left, right) => left.sortOrder - right.sortOrder)
      .filter((entry) => !isContactNameEntry(entry))
  );
}

/**
 * Drop duplicate Contact field values (case-insensitive trim) and duplicate empty
 * labeled slots (e.g. two empty Phone fields). Name entries are preserved as-is.
 */
export function dedupeContactEntries(entries: readonly CvStructuredEntry[]): CvStructuredEntry[] {
  const result: CvStructuredEntry[] = [];
  const seenValues = new Set<string>();
  const seenEmptyLabels = new Set<string>();

  for (const entry of entries) {
    if (isContactNameEntry(entry)) {
      result.push(entry);
      continue;
    }

    const valueKey = contactEntryValue(entry).toLowerCase();

    if (valueKey) {
      if (seenValues.has(valueKey)) {
        continue;
      }

      seenValues.add(valueKey);
      result.push(entry);
      continue;
    }

    const labelKey = entry.title.trim().toLowerCase();

    if (labelKey) {
      if (seenEmptyLabels.has(labelKey)) {
        continue;
      }

      seenEmptyLabels.add(labelKey);
    }

    result.push(entry);
  }

  return result;
}

export function contactFieldHasValue(entry: CvStructuredEntry): boolean {
  return contactEntryValue(entry).length > 0;
}

/** Preview/export: omit labeled Contact channels with empty values. */
export function contactFieldsWithValues(section: CvStructuredSection): readonly CvStructuredEntry[] {
  return contactFieldEntries(section).filter(contactFieldHasValue);
}

/** Light-touch ghost hint for an empty Contact value slot. */
export function contactValuePlaceholder(entry: CvStructuredEntry): string {
  switch (entry.title.trim().toLowerCase()) {
    case 'email':
      return 'name@example.com';
    case 'phone':
      return 'Phone number';
    case 'linkedin':
      return 'linkedin.com/in/…';
    default:
      return 'email, phone, link…';
  }
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
  return [
    createContactNameEntry(0),
    createContactFieldEntry(1, 'Email'),
    createContactFieldEntry(2, 'Phone'),
    createContactFieldEntry(3, 'LinkedIn')
  ];
}

/**
 * Labels that identify a Contact channel entry (aligned with BE
 * `IsKnownContactChannelLabel` in CvExportHtmlMapper).
 */
export function isKnownContactChannelLabel(title: string | null | undefined): boolean {
  if (!title || !title.trim()) {
    return false;
  }

  switch (title.trim().toLowerCase()) {
    case 'email':
    case 'e-mail':
    case 'phone':
    case 'mobile':
    case 'tel':
    case 'telephone':
    case 'linkedin':
    case 'location':
    case 'address':
    case 'website':
    case 'web':
    case 'url':
      return true;
    default:
      return false;
  }
}

/** True when a line looks like a channel value, not a person name (BE parity). */
export function isChannelShapedContactLine(value: string | null | undefined): boolean {
  if (!value || !value.trim()) {
    return false;
  }

  if (isKnownContactChannelLabel(value)) {
    return true;
  }

  const trimmed = value.trim();
  const lower = trimmed.toLowerCase();

  return (
    trimmed.includes('@') ||
    trimmed.includes('://') ||
    lower.startsWith('www.') ||
    lower.includes('linkedin.com/')
  );
}

/**
 * Import-legacy / pre-multi-entry Contact: one entry with multiple valued bullets
 * whose title is not a known channel label (person name, blank, or "Name").
 * Aligns with BE `TryExpandImportLegacyContact` (+ keeps Title=="Name" expand for drawer).
 */
export function isLegacyContactSection(section: CvStructuredSection): boolean {
  if (section.entries.length !== 1) {
    return false;
  }

  const only = section.entries[0];
  const valuedBullets = only.bullets.filter((line) => line.trim().length > 0);

  if (valuedBullets.length < 2) {
    return false;
  }

  // Known channel titles (Email/Phone/…) are modern multi-bullet entries, not import-legacy.
  return !isKnownContactChannelLabel(only.title);
}

function resolveLegacyContactName(entry: CvStructuredEntry): string | null {
  const subtitle = entry.subtitle?.trim();
  if (subtitle) {
    return subtitle;
  }

  const title = entry.title.trim();
  if (!title || title.toLowerCase() === 'name' || isChannelShapedContactLine(title)) {
    return null;
  }

  return title;
}

/**
 * Expands a legacy Contact entry (many bullets) into Name + one field per line.
 * Mutates `section.entries` in place; call on a draft clone.
 */
export function ensureModernContactShape(section: CvStructuredSection): CvStructuredSection {
  const sorted = [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);

  if (sorted.length === 0) {
    section.entries = createStarterContactEntries();
    return section;
  }

  if (isLegacyContactSection(section)) {
    const only = sorted[0];
    const lines = only.bullets.map((line) => line.trim()).filter((line) => line.length > 0);
    const nameSubtitle = resolveLegacyContactName(only);

    section.entries = [
      {
        ...createContactNameEntry(0),
        id: only.id,
        subtitle: nameSubtitle
      },
      // Real UUIDs only — synthetic `__ch` ids fail API Guid binding and sticky-draft equality.
      ...lines.map((line, index) => ({
        ...createContactFieldEntry(index + 1),
        id: crypto.randomUUID(),
        bullets: [line]
      }))
    ];
    return section;
  }

  if (!findContactNameEntry(section)) {
    section.entries = [createContactNameEntry(0), ...section.entries];
  }

  section.entries = dedupeContactEntries(
    [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder)
  ).map((entry, index) => ({ ...entry, sortOrder: index }));

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
