import {
  contactSectionForDisplay,
  ensureModernContactShape,
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
});
