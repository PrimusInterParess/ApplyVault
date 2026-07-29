import {
  CvSectionFieldCatalog,
  CvSectionType,
  CvStructuredEntry
} from '../models/cv-structured.model';
import { entryBodySourceText } from './cv-structured-edit-normalizer.util';

export function readEntryStringField(
  entry: CvStructuredEntry,
  sectionType: CvSectionType,
  field: CvSectionFieldCatalog
): string {
  switch (field.id) {
    case 'title':
    case 'groupTitle':
      return entry.title;
    case 'subtitle':
      return entry.subtitle ?? '';
    case 'dateRange':
      return entry.dateRange ?? '';
    case 'summary':
    case 'body':
      return field.id === 'body'
        ? entryBodySourceText(entry, sectionType)
        : entry.summary;
    case 'techStack':
      return entry.techStack;
    case 'skills':
      return readSkillsText(entry);
    default:
      return '';
  }
}

export function readEntryStringListField(entry: CvStructuredEntry, field: CvSectionFieldCatalog): string[] {
  if (field.id === 'skills') {
    return parseDelimitedList(readSkillsText(entry));
  }

  return [...entry.bullets];
}

export function patchEntryStringField(
  sectionType: CvSectionType,
  field: CvSectionFieldCatalog,
  value: string
): Partial<CvStructuredEntry> {
  switch (field.id) {
    case 'title':
    case 'groupTitle':
      return { title: value };
    case 'subtitle':
      return { subtitle: value.trim() ? value : null };
    case 'dateRange':
      return { dateRange: value.trim() ? value : null };
    case 'summary':
      return { summary: value };
    case 'body':
      return sectionType === 'Summary' ? { summary: value, title: '' } : { summary: value };
    case 'techStack':
      return { techStack: value };
    case 'skills':
      return { techStack: value, bullets: [] };
    default:
      return {};
  }
}

export function patchEntryStringListField(
  field: CvSectionFieldCatalog,
  values: string[]
): Partial<CvStructuredEntry> {
  const trimmed = values.map((value) => value.trim()).filter((value) => value.length > 0);

  if (field.id === 'skills') {
    return { techStack: trimmed.join(', '), bullets: [] };
  }

  return { bullets: values };
}

export function fieldPlaceholder(field: CvSectionFieldCatalog): string {
  const placeholders: Record<string, string> = {
    title: 'Job title, project, or degree',
    groupTitle: 'Skill group name',
    subtitle: 'Company or institution',
    dateRange: 'Jan 2020 – Present',
    summary: 'Short description',
    body: 'Write your professional summary',
    techStack: 'React, PostgreSQL, Azure',
    skills: 'React, TypeScript, Node.js',
    lines: 'email, phone, link…'
  };

  return placeholders[field.id] ?? '';
}

export function shouldRenderFieldsInRow(left: CvSectionFieldCatalog, right: CvSectionFieldCatalog | undefined): boolean {
  return left.id === 'subtitle' && right?.id === 'dateRange';
}

function readSkillsText(entry: CvStructuredEntry): string {
  const techStack = entry.techStack.trim();

  if (techStack) {
    return techStack;
  }

  const fromBullets = entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0);

  return fromBullets.length > 0 ? fromBullets.join(', ') : '';
}

function parseDelimitedList(value: string): string[] {
  return value
    .split(/[,;|]/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

export function skillsFieldUsesCommaInput(field: CvSectionFieldCatalog): boolean {
  return field.id === 'skills';
}

export function stringListUsesCommaInput(field: CvSectionFieldCatalog): boolean {
  return field.id === 'skills';
}
