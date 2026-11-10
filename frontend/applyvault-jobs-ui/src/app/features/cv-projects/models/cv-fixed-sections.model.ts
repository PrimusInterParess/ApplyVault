import { CvSectionType, CvStructuredEntry, CvStructuredSection } from './cv-structured.model';

export interface CvFixedSectionSlot {
  readonly sectionType: CvSectionType;
  readonly heading: string;
}

/** Catalog `fixedLayout` — Contact → Summary → Skills → Experience → Projects → Education → Custom. */
export const CV_FIXED_SECTIONS: readonly CvFixedSectionSlot[] = [
  { sectionType: 'Contact', heading: 'Contact' },
  { sectionType: 'Summary', heading: 'Summary' },
  { sectionType: 'Skills', heading: 'Skills' },
  { sectionType: 'Experience', heading: 'Experience' },
  { sectionType: 'Projects', heading: 'Projects' },
  { sectionType: 'Education', heading: 'Education' },
  { sectionType: 'Custom', heading: 'Certifications & Languages' }
] as const;

const KNOWN_SECTION_TYPES = new Set<string>([
  'Experience',
  'Projects',
  'Education',
  'Skills',
  'Summary',
  'Contact',
  'Custom'
]);

const CONTACT_HEADING = 'Contact';

/** Contact cannot be omitted. Missing and true mean included. */
export function sectionIncludedOnTemplate(
  section: Pick<CvStructuredSection, 'sectionType' | 'heading' | 'includedOnTemplate'>
): boolean {
  if (section.sectionType === 'Contact' || section.heading.trim().toLowerCase() === CONTACT_HEADING.toLowerCase()) {
    return true;
  }

  return section.includedOnTemplate !== false;
}

/**
 * Client safety net matching `FixedSectionLayout.Normalize`:
 * map by type (Custom/unknown → last slot), Contact heading wins, merge same-slot
 * entries (first id kept), create missing slots, force heading and sort order.
 * Copy `includedOnTemplate` from the first section that claims the slot.
 * A missing slot is included. Contact is always included.
 */
export function normalizeToFixedSections(
  sections: readonly CvStructuredSection[]
): CvStructuredSection[] {
  const slots = CV_FIXED_SECTIONS;
  const slotIndexByType = buildSlotIndex(slots);
  const customSlotIndex = slotIndexByType.get('Custom') ?? slots.length - 1;

  const idBySlot: Array<string | null> = slots.map(() => null);
  const idClaimedBySlot = slots.map(() => false);
  const includedBySlot: Array<boolean | null | undefined> = slots.map(() => null);
  const includedClaimedBySlot = slots.map(() => false);
  const entriesBySlot: CvStructuredEntry[][] = slots.map(() => []);

  for (const section of [...sections].sort((left, right) => left.sortOrder - right.sortOrder)) {
    const slotIndex = resolveSlotIndex(
      section.heading,
      section.sectionType,
      slotIndexByType,
      customSlotIndex
    );

    if (!idClaimedBySlot[slotIndex]) {
      idBySlot[slotIndex] = section.id;
      idClaimedBySlot[slotIndex] = true;
      includedBySlot[slotIndex] = section.includedOnTemplate;
      includedClaimedBySlot[slotIndex] = true;
    }

    entriesBySlot[slotIndex].push(
      ...[...section.entries]
        .sort((left, right) => left.sortOrder - right.sortOrder)
        .map((entry) => cloneEntry(entry))
    );
  }

  return slots.map((slot, index) => ({
    id: idBySlot[index] ?? crypto.randomUUID(),
    heading: slot.heading,
    sectionType: slot.sectionType,
    sortOrder: index,
    includedOnTemplate: resolveSlotIncluded(
      slot.sectionType,
      includedClaimedBySlot[index],
      includedBySlot[index]
    ),
    entries: entriesBySlot[index].map((entry, entryIndex) => ({
      ...entry,
      sortOrder: entryIndex
    }))
  }));
}

/** First claim wins. A missing slot is included. Contact is always included. */
function resolveSlotIncluded(
  sectionType: CvSectionType,
  claimed: boolean,
  includedOnTemplate: boolean | null | undefined
): boolean {
  const included = !claimed || includedOnTemplate !== false;
  return sectionType === 'Contact' || included;
}

function buildSlotIndex(slots: readonly CvFixedSectionSlot[]): Map<string, number> {
  const slotIndexByType = new Map<string, number>();

  for (let index = 0; index < slots.length; index++) {
    const type = slots[index].sectionType;
    if (!slotIndexByType.has(type)) {
      slotIndexByType.set(type, index);
    }
  }

  return slotIndexByType;
}

function resolveSlotIndex(
  heading: string | null | undefined,
  sectionType: string | null | undefined,
  slotIndexByType: Map<string, number>,
  customSlotIndex: number
): number {
  if (heading?.trim().toLowerCase() === CONTACT_HEADING.toLowerCase()) {
    const contactIndex = slotIndexByType.get('Contact');
    if (contactIndex !== undefined) {
      return contactIndex;
    }
  }

  const normalizedType = normalizeSectionType(sectionType);
  return slotIndexByType.get(normalizedType) ?? customSlotIndex;
}

function normalizeSectionType(value: string | null | undefined): CvSectionType {
  return value && KNOWN_SECTION_TYPES.has(value) ? (value as CvSectionType) : 'Custom';
}

function cloneEntry(entry: CvStructuredEntry): CvStructuredEntry {
  return {
    ...entry,
    bullets: [...entry.bullets],
    fields: { ...(entry.fields ?? {}) }
  };
}
