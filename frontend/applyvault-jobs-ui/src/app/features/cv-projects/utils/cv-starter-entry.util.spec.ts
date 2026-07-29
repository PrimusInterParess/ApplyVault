import { createBuilderStarterSections } from './cv-builder-starter-sections.util';
import {
  contactEntryValue,
  contactFieldHasValue,
  contactFieldsWithValues
} from './cv-contact-channels.util';
import {
  addStarterEntryToSection,
  createStarterContactEntries,
  createStarterEntryForSection
} from './cv-starter-entry.util';
import { contactSectionHasRenderableContent } from './cv-export-template-layout.util';
import { CvStructuredSection } from '../models/cv-structured.model';
import { createEmptySection } from './cv-structured-draft.util';

describe('cv-starter-entry.util', () => {
  it('creates Contact Starter Entries for Name, Email, Phone, and LinkedIn with empty values', () => {
    const entries = createStarterContactEntries();

    expect(entries.map((entry) => entry.title)).toEqual(['Name', 'Email', 'Phone', 'LinkedIn']);
    expect(entries.every((entry) => contactEntryValue(entry).length === 0)).toBeTrue();
    expect(entries.some((entry) => /alex|example|lorem/i.test(JSON.stringify(entry)))).toBeFalse();
  });

  it('creates Experience/Education/Projects Starter Entries with one empty bullet', () => {
    for (const sectionType of ['Experience', 'Education', 'Projects'] as const) {
      const entry = createStarterEntryForSection(sectionType, 0);

      expect(entry.bullets).toEqual(['']);
      expect(entry.title).toBe('');
      expect(entry.summary).toBe('');
    }
  });

  it('creates Skills Starter Entry as one empty group (skills input uses techStack + placeholder)', () => {
    const entry = createStarterEntryForSection('Skills', 0);

    expect(entry.title).toBe('');
    expect(entry.techStack).toBe('');
    expect(entry.bullets).toEqual([]);
  });

  it('creates unlabeled empty Contact field when adding a Contact Entry', () => {
    const entry = createStarterEntryForSection('Contact', 4);

    expect(entry.title).toBe('');
    expect(entry.bullets).toEqual(['']);
    expect(contactEntryValue(entry)).toBe('');
  });

  it('adds the same Starter Entry shape via addStarterEntryToSection', () => {
    const section: CvStructuredSection = {
      ...createEmptySection(0),
      sectionType: 'Experience',
      heading: 'Experience',
      entries: []
    };

    const next = addStarterEntryToSection(section);

    expect(next.entries.length).toBe(1);
    expect(next.entries[0].bullets).toEqual(['']);
  });

  it('builds Blank CV starter Sections Contact, Summary, Experience, Education, Skills', () => {
    const sections = createBuilderStarterSections();

    expect(sections.map((section) => section.sectionType)).toEqual([
      'Contact',
      'Summary',
      'Experience',
      'Education',
      'Skills'
    ]);

    const contact = sections[0];
    expect(contact.entries.map((entry) => entry.title)).toEqual([
      'Name',
      'Email',
      'Phone',
      'LinkedIn'
    ]);

    expect(sections.find((section) => section.sectionType === 'Experience')?.entries[0].bullets).toEqual([
      ''
    ]);
    expect(sections.find((section) => section.sectionType === 'Skills')?.entries[0].bullets).toEqual([]);
    expect(sections.find((section) => section.sectionType === 'Skills')?.entries[0].techStack).toBe('');
    expect(sections.find((section) => section.sectionType === 'Summary')?.entries.length).toBe(1);
  });
});

describe('Contact display filter', () => {
  it('omits empty-valued Contact channels while keeping filled ones', () => {
    const section: CvStructuredSection = {
      ...createEmptySection(0),
      sectionType: 'Contact',
      heading: 'Contact',
      entries: createStarterContactEntries()
    };

    section.entries[1].bullets = ['name@example.com'];
    section.entries[2].bullets = [''];

    expect(contactFieldHasValue(section.entries[1])).toBeTrue();
    expect(contactFieldHasValue(section.entries[2])).toBeFalse();
    expect(contactFieldsWithValues(section).map((entry) => entry.title)).toEqual(['Email']);
  });

  it('does not treat labeled-empty Contact channels as renderable export content', () => {
    const section: CvStructuredSection = {
      ...createEmptySection(0),
      sectionType: 'Contact',
      heading: 'Contact',
      entries: createStarterContactEntries()
    };

    expect(contactSectionHasRenderableContent(section)).toBeFalse();

    section.entries[1].bullets = ['name@example.com'];
    expect(contactSectionHasRenderableContent(section)).toBeTrue();
  });
});
