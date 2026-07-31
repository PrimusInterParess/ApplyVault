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
 * Canonical channel key for Contact labels (aliases → one bucket).
 * Aligns with BE `IsKnownContactChannelLabel` groupings used in edit dedupe.
 */
export function canonicalContactChannelLabel(title: string | null | undefined): string {
  if (!title || !title.trim()) {
    return '';
  }

  switch (title.trim().toLowerCase()) {
    case 'email':
    case 'e-mail':
      return 'email';
    case 'phone':
    case 'mobile':
    case 'tel':
    case 'telephone':
      return 'phone';
    case 'linkedin':
      return 'linkedin';
    case 'location':
    case 'address':
      return 'location';
    case 'website':
    case 'web':
    case 'url':
      return 'website';
    default:
      return title.trim().toLowerCase();
  }
}

/** Infer a starter channel label from an unlabeled valued line (email/phone/linkedin/web). */
export function inferContactChannelLabelFromValue(value: string | null | undefined): string | null {
  if (!value || !value.trim()) {
    return null;
  }

  const trimmed = value.trim();
  const lower = trimmed.toLowerCase();

  if (trimmed.includes('@')) {
    return 'email';
  }

  if (lower.includes('linkedin.com/')) {
    return 'linkedin';
  }

  if (trimmed.includes('://') || lower.startsWith('www.')) {
    return 'website';
  }

  const digits = trimmed.replace(/\D/g, '');

  if (digits.length >= 6 && /^[\d\s+()./\-]+$/.test(trimmed)) {
    return 'phone';
  }

  return null;
}

/**
 * Fold unlabeled valued Contact fields into empty labeled starter slots (Email/Phone/…)
 * when the value shape matches. Prevents Classic/Minimal edit canvas from showing both
 * a filled orphan line and an empty Email/Phone/LinkedIn ghost for the same channel.
 */
function absorbUnlabeledIntoEmptySlots(
  entries: readonly CvStructuredEntry[]
): CvStructuredEntry[] {
  const working = entries.map((entry) => ({
    ...entry,
    bullets: [...entry.bullets],
    fields: { ...(entry.fields ?? {}) }
  }));

  const emptySlotsByLabel = new Map<string, CvStructuredEntry>();

  for (const entry of working) {
    if (isContactNameEntry(entry) || contactEntryValue(entry)) {
      continue;
    }

    const label = canonicalContactChannelLabel(entry.title);

    if (!label || emptySlotsByLabel.has(label)) {
      continue;
    }

    emptySlotsByLabel.set(label, entry);
  }

  if (emptySlotsByLabel.size === 0) {
    return working;
  }

  const absorbedIds = new Set<string>();

  for (const entry of working) {
    if (isContactNameEntry(entry) || entry.title.trim() || !contactEntryValue(entry)) {
      continue;
    }

    const inferred = inferContactChannelLabelFromValue(contactEntryValue(entry));

    if (!inferred) {
      continue;
    }

    const slot = emptySlotsByLabel.get(inferred);

    if (!slot) {
      continue;
    }

    slot.bullets = [contactEntryValue(entry)];
    slot.summary = '';
    absorbedIds.add(entry.id);
    emptySlotsByLabel.delete(inferred);
  }

  if (absorbedIds.size === 0) {
    return working;
  }

  return working.filter((entry) => !absorbedIds.has(entry.id));
}

/**
 * Drop duplicate Contact field values (case-insensitive trim), duplicate empty
 * labeled slots, empty slots when a valued same-label channel already exists, and
 * absorb unlabeled valued orphans into empty starter slots. Keeps the first Name.
 */
export function dedupeContactEntries(entries: readonly CvStructuredEntry[]): CvStructuredEntry[] {
  const reconciled = absorbUnlabeledIntoEmptySlots(entries);
  const valuedLabels = new Set<string>();

  for (const entry of reconciled) {
    if (isContactNameEntry(entry) || !contactEntryValue(entry)) {
      continue;
    }

    const label = canonicalContactChannelLabel(entry.title);

    if (label) {
      valuedLabels.add(label);
    }
  }

  const result: CvStructuredEntry[] = [];
  const seenValues = new Set<string>();
  const seenEmptyLabels = new Set<string>();
  let keptName = false;

  for (const entry of reconciled) {
    if (isContactNameEntry(entry)) {
      if (keptName) {
        continue;
      }

      keptName = true;
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

    const labelKey = canonicalContactChannelLabel(entry.title);

    if (labelKey) {
      if (seenEmptyLabels.has(labelKey) || valuedLabels.has(labelKey)) {
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

function valuedContactBullets(entry: CvStructuredEntry): string[] {
  return entry.bullets.map((line) => line.trim()).filter((line) => line.length > 0);
}

/**
 * True when any Contact entry has multiple valued bullets that Content/export should
 * expand into one channel entry each (BE `NormalizeContactEntriesForExport` parity).
 */
export function hasValuedMultiBulletContactChannels(section: CvStructuredSection): boolean {
  return section.entries.some((entry) => valuedContactBullets(entry).length > 1);
}

/**
 * Expands import-legacy and valued multi-bullet Contact channels into Name + one
 * field per valued line (BE `NormalizeContactEntriesForExport` parity).
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
    const lines = valuedContactBullets(only);
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

    section.entries = dedupeContactEntries(
      [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder)
    ).map((entry, index) => ({ ...entry, sortOrder: index }));

    return section;
  }

  // Non-import-legacy: expand Name bullets + known-channel multi-bullet entries
  // into one valued field each (matches BE NormalizeContactEntriesForExport).
  const expanded: CvStructuredEntry[] = [];

  for (const entry of sorted) {
    if (isContactNameEntry(entry)) {
      expanded.push({
        ...entry,
        bullets: [],
        fields: { ...(entry.fields ?? {}) }
      });

      for (const bullet of valuedContactBullets(entry)) {
        expanded.push({
          ...createContactFieldEntry(expanded.length),
          id: crypto.randomUUID(),
          bullets: [bullet]
        });
      }

      continue;
    }

    const lines = valuedContactBullets(entry);

    if (lines.length > 1) {
      for (let index = 0; index < lines.length; index++) {
        expanded.push({
          ...(index === 0
            ? { ...entry, fields: { ...(entry.fields ?? {}) } }
            : createContactFieldEntry(expanded.length)),
          id: index === 0 ? entry.id : crypto.randomUUID(),
          // Keep original label on the first expanded field; rest unlabeled like BE.
          title: index === 0 ? entry.title : '',
          summary: '',
          bullets: [lines[index]],
          sortOrder: expanded.length
        });
      }

      continue;
    }

    expanded.push({
      ...entry,
      bullets: [...entry.bullets],
      fields: { ...(entry.fields ?? {}) }
    });
  }

  section.entries = expanded;

  if (!findContactNameEntry(section)) {
    section.entries = [createContactNameEntry(0), ...section.entries];
  }

  section.entries = dedupeContactEntries(
    [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder)
  ).map((entry, index) => ({ ...entry, sortOrder: index }));

  return section;
}

/**
 * Non-mutating view of Contact entries in the modern Name + fields shape.
 * Always runs ensureModernContactShape so absorb/dedupe applies even when the
 * draft still has starters + unlabeled orphans (Classic/Minimal edit canvas).
 */
export function contactSectionForDisplay(section: CvStructuredSection): CvStructuredSection {
  const draft: CvStructuredSection = {
    ...section,
    entries: section.entries.map((entry) => ({
      ...entry,
      bullets: [...entry.bullets],
      fields: { ...(entry.fields ?? {}) }
    }))
  };

  return ensureModernContactShape(draft);
}
