import type {
  HiringManagerContact,
  HiringManagerContactType
} from '../../shared/models/scrapeResult';
import {
  HIRING_MANAGER_LABELS,
  HIRING_MANAGER_SELECTORS
} from './constants';
import {
  extractNameFromEmailAddress,
  isLikelyPersonName,
  scorePersonNameAgainstEmail
} from './metadata';
import {
  addFieldCandidate,
  extractLabeledValue,
  getNormalizedText,
  getTextFromElement,
  pickBestCandidate
} from './shared';
import type { ContactExtractionOptions, FieldCandidate } from './types';

function buildContact(
  type: HiringManagerContactType,
  value: string,
  label?: string
): HiringManagerContact | undefined {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue) {
    return undefined;
  }

  return {
    type,
    value: normalizedValue,
    label: getNormalizedText(label)
  };
}

function dedupeContacts(contacts: HiringManagerContact[]): HiringManagerContact[] {
  const seenContacts = new Set<string>();

  return contacts.filter((contact) => {
    const key = `${contact.type}:${contact.value.toLowerCase()}`;

    if (seenContacts.has(key)) {
      return false;
    }

    seenContacts.add(key);
    return true;
  });
}

function isLikelyHiringContactAnchor(
  anchor: HTMLAnchorElement,
  hiringSection?: Element,
  restrictLinkedInProfiles?: boolean
): boolean {
  if (!restrictLinkedInProfiles) {
    return true;
  }

  if (hiringSection?.contains(anchor)) {
    return true;
  }

  const contextualContainer = anchor.closest('section, article, div');
  const contextText = getNormalizedText(contextualContainer?.textContent)?.toLowerCase();

  return Boolean(contextText && /(hiring|recruiter|talent|contact)/.test(contextText));
}

function getMailtoEmailAddress(href: string): string | undefined {
  const mailtoValue = href.replace(/^mailto:/i, '').trim();

  if (!mailtoValue) {
    return undefined;
  }

  const [addressPart] = mailtoValue.split('?');
  const normalizedAddress = getNormalizedText(addressPart);

  if (!normalizedAddress || !/^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i.test(normalizedAddress)) {
    return undefined;
  }

  return normalizedAddress;
}

function getTextWindowsAroundValue(text: string, value: string, radius = 160): string[] {
  const windows: string[] = [];
  const normalizedText = text.toLowerCase();
  const normalizedValue = value.toLowerCase();
  let startIndex = 0;

  while (windows.length < 3) {
    const matchIndex = normalizedText.indexOf(normalizedValue, startIndex);

    if (matchIndex === -1) {
      break;
    }

    const sliceStart = Math.max(0, matchIndex - radius);
    const sliceEnd = Math.min(text.length, matchIndex + value.length + radius);
    windows.push(text.slice(sliceStart, sliceEnd));
    startIndex = matchIndex + normalizedValue.length;
  }

  return windows;
}

function extractBestPersonNameFromContext(contextText: string, email: string): string | undefined {
  const emailIndex = contextText.toLowerCase().indexOf(email.toLowerCase());

  if (emailIndex === -1) {
    return undefined;
  }

  const candidatePattern = /\b([A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+(?:\s+[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+){1,3})\b/g;
  let bestCandidate: FieldCandidate | undefined;

  for (const match of contextText.matchAll(candidatePattern)) {
    const candidateValue = getNormalizedText(match[1]);
    const candidateIndex = match.index ?? -1;

    if (!candidateValue || candidateIndex === -1 || !isLikelyPersonName(candidateValue)) {
      continue;
    }

    const overlapScore = scorePersonNameAgainstEmail(candidateValue, email);
    const distancePenalty = Math.abs(emailIndex - candidateIndex) / 400;
    const nearbyContext = contextText.slice(
      Math.max(0, candidateIndex - 40),
      Math.min(contextText.length, candidateIndex + candidateValue.length + 40)
    );
    const roleBonus = /\b(contact|recruiter|manager|head|director|lead|talent|spørgsmål|kontakt)\b/i.test(
      nearbyContext
    )
      ? 0.2
      : 0;
    const confidence = overlapScore + roleBonus - distancePenalty;

    if (confidence <= 0.2) {
      continue;
    }

    if (!bestCandidate || confidence > bestCandidate.confidence) {
      bestCandidate = {
        value: candidateValue,
        confidence,
        source: 'contact-context'
      };
    }
  }

  return bestCandidate?.value;
}

function getContextTextsForEmail(documentRef: Document, email: string): string[] {
  const contexts: string[] = [];
  const seenContexts = new Set<string>();

  for (const anchor of Array.from(documentRef.querySelectorAll<HTMLAnchorElement>('a[href^="mailto:"]'))) {
    const anchorEmail = getMailtoEmailAddress(anchor.getAttribute('href') || '');

    if (!anchorEmail || anchorEmail.toLowerCase() !== email.toLowerCase()) {
      continue;
    }

    const contextualElements = [
      anchor.closest('p, li, div, section, article'),
      anchor.parentElement,
      anchor.parentElement?.parentElement
    ].filter((element, index, all): element is Element => Boolean(element) && all.indexOf(element) === index);

    for (const element of contextualElements) {
      const text = getTextFromElement(element);

      if (!text) {
        continue;
      }

      const key = text.toLowerCase();

      if (seenContexts.has(key)) {
        continue;
      }

      seenContexts.add(key);
      contexts.push(text);
    }
  }

  return contexts;
}

export function extractContacts(
  documentRef: Document,
  text: string,
  options?: ContactExtractionOptions
): HiringManagerContact[] {
  const contacts: HiringManagerContact[] = [];

  for (const anchor of Array.from(documentRef.querySelectorAll<HTMLAnchorElement>('a[href]'))) {
    const href = anchor.getAttribute('href') || '';
    const label = getTextFromElement(anchor);

    if (href.startsWith('mailto:')) {
      const emailAddress = getMailtoEmailAddress(href);
      const contact = emailAddress ? buildContact('email', emailAddress, label) : undefined;

      if (contact) {
        contacts.push(contact);
      }
    }

    if (href.startsWith('tel:')) {
      const contact = buildContact('phone', href.replace(/^tel:/i, ''), label);

      if (contact) {
        contacts.push(contact);
      }
    }

    if (/linkedin\.com\/in\//i.test(href)) {
      if (!isLikelyHiringContactAnchor(anchor, options?.hiringSection, options?.restrictLinkedInProfiles)) {
        continue;
      }

      const contact = buildContact('linkedin', href, label);

      if (contact) {
        contacts.push(contact);
      }
    }
  }

  const emailMatches = text.match(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi) ?? [];
  const phoneMatches = text.match(/(?:\+?\d[\d\s().-]{7,}\d)/g) ?? [];

  for (const email of emailMatches.slice(0, 5)) {
    const contact = buildContact('email', email);

    if (contact) {
      contacts.push(contact);
    }
  }

  for (const phone of phoneMatches.slice(0, 5)) {
    const contact = buildContact('phone', phone);

    if (contact) {
      contacts.push(contact);
    }
  }

  return dedupeContacts(contacts);
}

export function extractHiringManagerName(documentRef: Document, lines: string[]): string | undefined {
  for (const selector of HIRING_MANAGER_SELECTORS) {
    const name = getTextFromElement(documentRef.querySelector(selector));

    if (name && isLikelyPersonName(name)) {
      return name;
    }
  }

  const labeledName = extractLabeledValue(lines, HIRING_MANAGER_LABELS);
  return labeledName && isLikelyPersonName(labeledName) ? labeledName : undefined;
}

export function extractHiringManagerNameFromContacts(
  documentRef: Document,
  text: string,
  contacts: HiringManagerContact[]
): string | undefined {
  const candidates: FieldCandidate[] = [];

  for (const contact of contacts) {
    if (contact.type !== 'email') {
      continue;
    }

    for (const contextText of getContextTextsForEmail(documentRef, contact.value)) {
      addFieldCandidate(
        candidates,
        extractBestPersonNameFromContext(contextText, contact.value),
        0.98,
        'contact-dom-context',
        isLikelyPersonName
      );
    }

    for (const textWindow of getTextWindowsAroundValue(text, contact.value)) {
      addFieldCandidate(
        candidates,
        extractBestPersonNameFromContext(textWindow, contact.value),
        0.94,
        'contact-text-window',
        isLikelyPersonName
      );
    }

    const escapedEmail = contact.value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const emailPattern = new RegExp(
      `(?:contact(?:[^\\n.]{0,120})?|questions?(?:[^\\n.]{0,120})?)\\b([A-Z][A-Za-zÀ-ÿ' -]{1,80})\\s*(?:,|\\(|-)\\s*${escapedEmail}`,
      'i'
    );
    const emailMatch = text.match(emailPattern);

    if (emailMatch?.[1]) {
      const personName = getNormalizedText(emailMatch[1]);
      addFieldCandidate(candidates, personName, 0.9, 'contact-regex', isLikelyPersonName);
    }

    addFieldCandidate(candidates, extractNameFromEmailAddress(contact.value), 0.78, 'email-local-part', isLikelyPersonName);
  }

  return pickBestCandidate(candidates);
}

export function extractHiringManagerNameFromSection(section: Element | undefined): string | undefined {
  if (!section) {
    return undefined;
  }

  const directCandidate = getTextFromElement(section.querySelector('a[href*="/in/"], strong, h3'));

  if (directCandidate && isLikelyPersonName(directCandidate)) {
    return directCandidate;
  }

  const sectionLines = (section.textContent || '')
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

  const labeledName = extractLabeledValue(sectionLines, HIRING_MANAGER_LABELS);
  return labeledName && isLikelyPersonName(labeledName) ? labeledName : undefined;
}
