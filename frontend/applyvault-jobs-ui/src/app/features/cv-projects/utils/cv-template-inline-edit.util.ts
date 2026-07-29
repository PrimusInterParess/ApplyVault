import {
  ensureModernContactShape,
  findContactNameEntry,
  isContactNameEntry
} from './cv-contact-channels.util';
import {
  cloneSectionForDraft,
  normalizeEntrySortOrders,
  removeEntryFromSection,
  removeSectionById,
  reorderSections
} from './cv-structured-draft.util';
import {
  addStarterEntryToSection,
  appendSectionOfType,
  createStarterEntryForSection
} from './cv-starter-entry.util';
import { CvSectionType, CvStructuredSection } from '../models/cv-structured.model';

export type CvTemplateInlineEdit =
  | { kind: 'sectionHeading'; sectionId: string; value: string }
  | {
      kind: 'entryField';
      sectionId: string;
      entryId: string;
      field: 'title' | 'subtitle' | 'dateRange' | 'summary' | 'techStack';
      value: string;
    }
  | { kind: 'bullet'; sectionId: string; entryId: string; index: number; value: string }
  | { kind: 'skillsLine'; sectionId: string; entryId: string; value: string }
  | { kind: 'addEntry'; sectionId: string }
  | { kind: 'removeEntry'; sectionId: string; entryId: string }
  | { kind: 'addSection'; sectionType: CvSectionType }
  | { kind: 'removeSection'; sectionId: string }
  | { kind: 'reorderSections'; fromIndex: number; toIndex: number };

const MULTI_ENTRY_SECTION_TYPES = new Set([
  'Experience',
  'Education',
  'Projects',
  'Skills',
  'Custom',
  'Contact'
]);

/** Catalog section types offered when adding a Section (Contact only when missing). */
export const ADDABLE_SECTION_TYPES: readonly CvSectionType[] = [
  'Contact',
  'Summary',
  'Experience',
  'Projects',
  'Education',
  'Skills',
  'Custom'
] as const;

const UNIQUE_SECTION_TYPES = new Set<CvSectionType>(['Contact', 'Summary']);

export function sectionAllowsMultipleEntries(sectionType: string): boolean {
  return MULTI_ENTRY_SECTION_TYPES.has(sectionType);
}

export function addEntryLabelForSection(sectionType: string): string {
  switch (sectionType) {
    case 'Experience':
      return 'Add another role';
    case 'Education':
      return 'Add another degree';
    case 'Projects':
      return 'Add another project';
    case 'Skills':
      return 'Add skill group';
    case 'Contact':
      return 'Add contact';
    default:
      return 'Add another item';
  }
}

export function canAddSectionType(
  sections: readonly CvStructuredSection[],
  sectionType: CvSectionType
): boolean {
  if (!UNIQUE_SECTION_TYPES.has(sectionType)) {
    return true;
  }

  return !sections.some((section) => section.sectionType === sectionType);
}

export function addableSectionTypes(
  sections: readonly CvStructuredSection[]
): readonly CvSectionType[] {
  return ADDABLE_SECTION_TYPES.filter((sectionType) => canAddSectionType(sections, sectionType));
}

export function applyCvTemplateInlineEdit(
  sections: readonly CvStructuredSection[],
  edit: CvTemplateInlineEdit
): CvStructuredSection[] {
  if (edit.kind === 'addSection') {
    if (!canAddSectionType(sections, edit.sectionType)) {
      return sections.map((section) => cloneSectionForDraft(section));
    }

    return appendSectionOfType(sections, edit.sectionType);
  }

  if (edit.kind === 'removeSection') {
    return removeSectionById(sections, edit.sectionId);
  }

  if (edit.kind === 'reorderSections') {
    if (
      edit.fromIndex === edit.toIndex ||
      edit.fromIndex < 0 ||
      edit.toIndex < 0 ||
      edit.fromIndex >= sections.length ||
      edit.toIndex >= sections.length
    ) {
      return sections.map((section) => cloneSectionForDraft(section));
    }

    return reorderSections(sections, edit.fromIndex, edit.toIndex);
  }

  return sections.map((section) => {
    if (section.id !== edit.sectionId) {
      return cloneSectionForDraft(section);
    }

    const draft =
      section.sectionType === 'Contact'
        ? ensureModernContactShape(cloneSectionForDraft(section))
        : cloneSectionForDraft(section);

    if (edit.kind === 'sectionHeading') {
      draft.heading = edit.value;
      return draft;
    }

    if (edit.kind === 'addEntry') {
      if (!sectionAllowsMultipleEntries(draft.sectionType)) {
        return draft;
      }

      if (draft.sectionType === 'Contact') {
        return addContactFieldToSection(draft);
      }

      return addStarterEntryToSection(draft);
    }

    if (edit.kind === 'removeEntry') {
      if (draft.sectionType === 'Contact') {
        return removeContactEntry(draft, edit.entryId);
      }

      if (!sectionAllowsMultipleEntries(draft.sectionType) || draft.entries.length <= 1) {
        return draft;
      }

      return removeEntryFromSection(draft, edit.entryId);
    }

    const entryIndex = draft.entries.findIndex((entry) => entry.id === edit.entryId);

    if (entryIndex < 0) {
      return draft;
    }

    const entry = { ...draft.entries[entryIndex], bullets: [...draft.entries[entryIndex].bullets] };

    if (edit.kind === 'entryField') {
      if (edit.field === 'subtitle' || edit.field === 'dateRange') {
        entry[edit.field] = edit.value.length > 0 ? edit.value : null;
      } else {
        entry[edit.field] = edit.value;
      }
    } else if (edit.kind === 'bullet') {
      while (entry.bullets.length <= edit.index) {
        entry.bullets.push('');
      }

      entry.bullets[edit.index] = edit.value;
    } else if (edit.kind === 'skillsLine') {
      const items = edit.value
        .split(/[,;|]/)
        .map((item) => item.trim())
        .filter((item) => item.length > 0);

      entry.bullets = items;
      entry.techStack = items.join(', ');
    }

    draft.entries = draft.entries.map((item, index) => (index === entryIndex ? entry : item));
    normalizeEntrySortOrders(draft.entries);
    return draft;
  });
}

function addContactFieldToSection(section: CvStructuredSection): CvStructuredSection {
  const draft = cloneSectionForDraft(section);
  ensureModernContactShape(draft);
  const next = createStarterEntryForSection('Contact', draft.entries.length);
  draft.entries = [...draft.entries, next];
  normalizeEntrySortOrders(draft.entries);
  return draft;
}

function removeContactEntry(section: CvStructuredSection, entryId: string): CvStructuredSection {
  const target = section.entries.find((entry) => entry.id === entryId);

  if (!target || isContactNameEntry(target)) {
    return cloneSectionForDraft(section);
  }

  return removeEntryFromSection(section, entryId);
}

export function resolveContactDisplayName(section: CvStructuredSection | null | undefined): string {
  if (!section) {
    return 'Your name';
  }

  const nameEntry = findContactNameEntry(section);

  if (!nameEntry) {
    return 'Your name';
  }

  const fromSubtitle = nameEntry.subtitle?.trim();

  if (fromSubtitle) {
    return fromSubtitle;
  }

  return 'Your name';
}
