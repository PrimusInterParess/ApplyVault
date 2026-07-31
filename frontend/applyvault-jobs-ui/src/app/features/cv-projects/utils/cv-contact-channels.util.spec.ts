import {
  contactEntryValue,
  contactFieldEntries,
  contactFieldHasValue,
  contactSectionForDisplay,
  createStarterContactEntries,
  dedupeContactEntries,
  ensureModernContactShape,
  hasValuedMultiBulletContactChannels,
  isKnownContactChannelLabel,
  isLegacyContactSection
} from './cv-contact-channels.util';
import { CvStructuredEntry, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from './cv-structured-draft.util';

function contactSection(entries: CvStructuredEntry[]): CvStructuredSection {
  return {
    ...createEmptySection(0),
    sectionType: 'Contact',
    heading: 'Contact',
    entries
  };
}

function legacyImportEntry(title: string, bullets: string[], subtitle: string | null = null): CvStructuredEntry {
  return {
    ...createEmptyEntry(0),
    title,
    subtitle,
    bullets
  };
}

describe('cv-contact-channels.util legacy Contact', () => {
  it('detects import-legacy person-name title + multi-bullet channels', () => {
    const section = contactSection([
      legacyImportEntry('Alex Rivera', ['alex@example.com', '+1 555 0100', 'linkedin.com/in/alex'])
    ]);

    expect(isLegacyContactSection(section)).toBeTrue();
  });

  it('still detects blank / Name title multi-bullet as legacy', () => {
    expect(
      isLegacyContactSection(contactSection([legacyImportEntry('', ['a@b.com', '555'])]))
    ).toBeTrue();
    expect(
      isLegacyContactSection(
        contactSection([legacyImportEntry('Name', ['a@b.com', '555'], 'Alex Rivera')])
      )
    ).toBeTrue();
  });

  it('does not treat known channel label + multi-bullet as import-legacy', () => {
    expect(isKnownContactChannelLabel('Email')).toBeTrue();
    expect(
      isLegacyContactSection(contactSection([legacyImportEntry('Email', ['a@b.com', 'other@b.com'])]))
    ).toBeFalse();
  });

  it('contactSectionForDisplay expands import-legacy to Name + one field per channel', () => {
    const section = contactSection([
      legacyImportEntry('Alex Rivera', ['alex@example.com', '+1 555 0100', 'linkedin.com/in/alex'])
    ]);

    const view = contactSectionForDisplay(section);

    expect(view.entries.map((entry) => entry.title)).toEqual(['Name', '', '', '']);
    expect(view.entries[0].subtitle).toBe('Alex Rivera');
    expect(view.entries.slice(1).map((entry) => entry.bullets[0])).toEqual([
      'alex@example.com',
      '+1 555 0100',
      'linkedin.com/in/alex'
    ]);
    // Source section unchanged (non-mutating view).
    expect(section.entries.length).toBe(1);
  });

  it('ensureModernContactShape prefers subtitle over person-name title', () => {
    const section = contactSection([
      legacyImportEntry('Ignored Title', ['a@b.com', '555'], 'Preferred Name')
    ]);

    ensureModernContactShape(section);

    expect(section.entries[0].title).toBe('Name');
    expect(section.entries[0].subtitle).toBe('Preferred Name');
    expect(section.entries.length).toBe(3);
  });

  it('ensureModernContactShape assigns real UUIDs to expanded channel entries (never __ch)', () => {
    const section = contactSection([
      legacyImportEntry('Alex Rivera', ['alex@example.com', '+1 555 0100'])
    ]);
    const uuidRe =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

    ensureModernContactShape(section);

    const channelIds = section.entries.slice(1).map((entry) => entry.id);
    expect(channelIds.length).toBe(2);
    expect(channelIds.every((id) => uuidRe.test(id))).toBeTrue();
    expect(channelIds.some((id) => id.includes('__ch'))).toBeFalse();
  });

  it('does not promote channel-shaped title to Name subtitle', () => {
    const section = contactSection([
      legacyImportEntry('alex@example.com', ['+1 555 0100', 'linkedin.com/in/alex'])
    ]);

    ensureModernContactShape(section);

    expect(section.entries[0].subtitle).toBeNull();
    expect(section.entries.slice(1).map((entry) => entry.bullets[0])).toEqual([
      '+1 555 0100',
      'linkedin.com/in/alex'
    ]);
  });

  it('detects known-channel multi-bullet as valued expand (not import-legacy)', () => {
    const section = contactSection([
      {
        ...createEmptyEntry(0),
        title: 'Name',
        subtitle: 'Alex',
        bullets: []
      },
      {
        ...createEmptyEntry(1),
        title: 'Email',
        bullets: ['a@b.com', 'other@b.com']
      }
    ]);

    expect(isLegacyContactSection(section)).toBeFalse();
    expect(hasValuedMultiBulletContactChannels(section)).toBeTrue();
  });

  it('ensureModernContactShape expands known-channel multi-bullet into one field per value', () => {
    const section = contactSection([
      {
        ...createEmptyEntry(0),
        title: 'Name',
        subtitle: 'Alex',
        bullets: []
      },
      {
        ...createEmptyEntry(1),
        id: 'email-1',
        title: 'Email',
        bullets: ['a@b.com', 'other@b.com']
      },
      {
        ...createEmptyEntry(2),
        title: 'Phone',
        bullets: ['']
      }
    ]);

    ensureModernContactShape(section);

    expect(section.entries.map((entry) => entry.title)).toEqual(['Name', 'Email', '', 'Phone']);
    expect(section.entries[1].id).toBe('email-1');
    expect(section.entries[1].bullets).toEqual(['a@b.com']);
    expect(section.entries[2].bullets).toEqual(['other@b.com']);
    expect(section.entries[3].title).toBe('Phone');
    expect(contactFieldHasValue(section.entries[3])).toBeFalse();
  });

  it('contactSectionForDisplay expands valued multi-bullet without mutating source', () => {
    const section = contactSection([
      {
        ...createEmptyEntry(0),
        title: 'Name',
        subtitle: 'Alex',
        bullets: []
      },
      {
        ...createEmptyEntry(1),
        title: 'Email',
        bullets: ['a@b.com', 'other@b.com']
      }
    ]);

    const view = contactSectionForDisplay(section);

    expect(view.entries.slice(1).map((entry) => entry.bullets[0])).toEqual([
      'a@b.com',
      'other@b.com'
    ]);
    expect(section.entries.length).toBe(2);
    expect(section.entries[1].bullets).toEqual(['a@b.com', 'other@b.com']);
  });

  it('keeps empty Contact field slots when expanding multi-bullet siblings', () => {
    const section = contactSection([
      {
        ...createEmptyEntry(0),
        title: 'Name',
        subtitle: null,
        bullets: []
      },
      {
        ...createEmptyEntry(1),
        title: 'Email',
        bullets: ['a@b.com', 'b@b.com']
      },
      {
        ...createEmptyEntry(2),
        title: 'LinkedIn',
        bullets: ['']
      }
    ]);

    ensureModernContactShape(section);

    const linkedIn = section.entries.find((entry) => entry.title.trim().toLowerCase() === 'linkedin');
    expect(linkedIn).toBeTruthy();
    expect(contactFieldHasValue(linkedIn!)).toBeFalse();
  });

  it('absorbs Name-bullet channels into empty Email/Phone/LinkedIn starters (no Classic dups)', () => {
    const starters = createStarterContactEntries();
    starters[0] = {
      ...starters[0],
      subtitle: 'Alex Rivera',
      bullets: ['alex@example.com', '+45 12 34 56 78', 'linkedin.com/in/alex']
    };

    const section = contactSection(starters);
    ensureModernContactShape(section);

    const fields = contactFieldEntries(section);
    expect(fields.map((entry) => entry.title.trim().toLowerCase())).toEqual([
      'email',
      'phone',
      'linkedin'
    ]);
    expect(fields.map((entry) => entry.bullets[0])).toEqual([
      'alex@example.com',
      '+45 12 34 56 78',
      'linkedin.com/in/alex'
    ]);
  });

  it('dedupeContactEntries drops empty labeled slot when valued same-label exists', () => {
    const entries = dedupeContactEntries([
      { ...createEmptyEntry(0), title: 'Name', subtitle: 'Alex' },
      { ...createEmptyEntry(1), title: 'Email', bullets: ['a@b.com'], sortOrder: 1 },
      { ...createEmptyEntry(2), title: 'Email', bullets: [''], sortOrder: 2 },
      { ...createEmptyEntry(3), title: 'Phone', bullets: [''], sortOrder: 3 },
      { ...createEmptyEntry(4), title: 'LinkedIn', bullets: [''], sortOrder: 4 }
    ]);

    const fields = entries.filter((entry) => entry.title.trim().toLowerCase() !== 'name');
    const emails = fields.filter((entry) => entry.title.trim().toLowerCase() === 'email');
    expect(emails.length).toBe(1);
    expect(emails[0]?.bullets[0]).toBe('a@b.com');
    expect(fields.map((entry) => entry.title.trim().toLowerCase())).toEqual([
      'email',
      'phone',
      'linkedin'
    ]);
  });

  it('contactSectionForDisplay always absorbs unlabeled orphans into starters', () => {
    const section = contactSection([
      { ...createEmptyEntry(0), title: 'Name', subtitle: 'Alex', bullets: [] },
      { ...createEmptyEntry(1), title: '', bullets: ['alex@example.com'], sortOrder: 1 },
      { ...createEmptyEntry(2), title: '', bullets: ['+45 12 34 56 78'], sortOrder: 2 },
      { ...createEmptyEntry(3), title: 'Email', bullets: [''], sortOrder: 3 },
      { ...createEmptyEntry(4), title: 'Phone', bullets: [''], sortOrder: 4 },
      { ...createEmptyEntry(5), title: 'LinkedIn', bullets: [''], sortOrder: 5 }
    ]);

    const view = contactSectionForDisplay(section);
    const fields = contactFieldEntries(view);

    expect(fields.length).toBe(3);
    expect(fields.map((entry) => entry.title.trim().toLowerCase())).toEqual([
      'email',
      'phone',
      'linkedin'
    ]);
    expect(fields.map((entry) => contactEntryValue(entry))).toEqual([
      'alex@example.com',
      '+45 12 34 56 78',
      ''
    ]);
  });
});
