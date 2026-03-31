import {
  HIRING_MANAGER_LABELS,
  NON_METADATA_TERMS,
  NON_PERSON_EMAIL_LOCALPART_TERMS,
  NON_PERSON_HIRING_MANAGER_TERMS
} from './constants';
import { addFieldCandidate, collectHeaderMetadataContainers, getNormalizedText, getTextFromElement, splitMetadataText } from './shared';
import type { FieldCandidate } from './types';

export function isLikelyActionText(value: string): boolean {
  return /^(apply|ansøg|search|share|save)\b|^(deadline|tiltrædelse|start date|salary|løn|grundløn|compensation|pay|job description|description|contact|email|phone|forventninger)\b/i.test(
    value
  );
}

export function isDisallowedMetadataValue(value: string): boolean {
  const normalizedValue = value.toLowerCase();

  return (
    NON_METADATA_TERMS.some((term) => normalizedValue === term || normalizedValue.includes(term)) ||
    HIRING_MANAGER_LABELS.some((label) => normalizedValue === label || normalizedValue.startsWith(`${label}:`))
  );
}

export function isLikelyLocationValue(value: string): boolean {
  if (value.length < 2 || value.length > 140) {
    return false;
  }

  if (
    /@|https?:\/\//i.test(value) ||
    isLikelyActionText(value) ||
    isDisallowedMetadataValue(value) ||
    /\b(?:salary|løn|grundløn|compensation|pay|pension|bonus|benefits|forventninger)\b/i.test(value)
  ) {
    return false;
  }

  if (/\b(remote|hybrid|onsite|on-site|denmark|danmark|sweden|norway|germany|europe|emea|apac)\b/i.test(value)) {
    return true;
  }

  if (/\b\d{4,5}\b/.test(value)) {
    return true;
  }

  if (/,/.test(value)) {
    return true;
  }

  if (/\b(?:street|st\.|road|rd\.|avenue|ave\.|vej|gade|plads|all[eé]|boulevard|blvd)\b/i.test(value)) {
    return true;
  }

  const words = value.split(/\s+/);

  if (words.length === 1) {
    return /^[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+$/.test(words[0]);
  }

  if (words.length > 4 || !/^[A-Za-zÀ-ÿ0-9 .,'()/-]+$/.test(value)) {
    return false;
  }

  const normalizedWords = words
    .map((word) => word.replace(/^[^A-Za-zÀ-ÿ0-9]+|[^A-Za-zÀ-ÿ0-9]+$/g, ''))
    .filter(Boolean);

  if (normalizedWords.length === 0) {
    return false;
  }

  const capitalizedOrUppercaseWords = normalizedWords.filter(
    (word) => /^[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]*$/.test(word) || /^[A-Z]{2,}$/.test(word)
  ).length;

  return capitalizedOrUppercaseWords === normalizedWords.length;
}

export function isLikelyCompanyValue(value: string): boolean {
  if (value.length < 2 || value.length > 100) {
    return false;
  }

  if (/@|https?:\/\//i.test(value) || isLikelyActionText(value) || isDisallowedMetadataValue(value)) {
    return false;
  }

  if (/^\d+$/.test(value)) {
    return false;
  }

  if (isLikelyLocationValue(value) && /\d/.test(value)) {
    return false;
  }

  return value.split(/\s+/).length <= 8;
}

export function isLikelyPersonName(value: string): boolean {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue || normalizedValue.length < 3 || normalizedValue.length > 80) {
    return false;
  }

  const normalizedLower = normalizedValue.toLowerCase();

  if (
    /@|https?:\/\/|\d/.test(normalizedValue) ||
    NON_PERSON_HIRING_MANAGER_TERMS.some((term) => normalizedLower.includes(term)) ||
    HIRING_MANAGER_LABELS.some((label) => normalizedLower === label || normalizedLower.startsWith(`${label}:`))
  ) {
    return false;
  }

  const words = normalizedValue
    .replace(/[(),]/g, ' ')
    .split(/\s+/)
    .filter(Boolean);

  if (words.length < 2 || words.length > 5) {
    return false;
  }

  const capitalizedWordCount = words.filter((word) => /^[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+$/.test(word)).length;

  return capitalizedWordCount >= 2;
}

export function getEmailLocalPartTokens(email: string): string[] {
  const [localPart] = email.toLowerCase().split('@');

  return localPart
    .split(/[._-]+/)
    .map((token) => token.trim())
    .filter(
      (token) =>
        token.length >= 2 && /^[a-zà-ÿ]+$/i.test(token) && !NON_PERSON_EMAIL_LOCALPART_TERMS.includes(token)
    );
}

export function getNameTokens(value: string): string[] {
  return value
    .toLowerCase()
    .split(/[^a-zà-ÿ'-]+/i)
    .map((token) => token.trim())
    .filter(Boolean);
}

export function extractNameFromEmailAddress(email: string): string | undefined {
  const tokens = getEmailLocalPartTokens(email);

  if (tokens.length < 2 || tokens.length > 4) {
    return undefined;
  }

  const formattedName = tokens.map((token) => `${token.charAt(0).toUpperCase()}${token.slice(1)}`).join(' ');
  return isLikelyPersonName(formattedName) ? formattedName : undefined;
}

export function scorePersonNameAgainstEmail(name: string, email: string): number {
  const emailTokens = getEmailLocalPartTokens(email);

  if (emailTokens.length === 0) {
    return 0;
  }

  const nameTokens = new Set(getNameTokens(name));
  let overlapCount = 0;

  for (const token of emailTokens) {
    if (nameTokens.has(token)) {
      overlapCount += 1;
    }
  }

  return overlapCount / emailTokens.length;
}

export function collectHeaderMetadataCandidates(
  documentRef: Document,
  titleValue: string | undefined
): FieldCandidate[] {
  const { titleElement, containers } = collectHeaderMetadataContainers(documentRef);

  if (!titleElement) {
    return [];
  }

  const candidates: FieldCandidate[] = [];
  const seenTexts = new Set<string>();

  for (const container of containers) {
    const elements = Array.from(container.querySelectorAll('p, span, a, li, strong, div')).slice(0, 40);

    for (const element of elements) {
      if (element === titleElement || element.querySelector('h1, h2')) {
        continue;
      }

      const text = getTextFromElement(element);

      if (!text || text === titleValue || text.length > 140) {
        continue;
      }

      const textKey = text.toLowerCase();

      if (seenTexts.has(textKey)) {
        continue;
      }

      seenTexts.add(textKey);

      const parts = splitMetadataText(text);

      if (parts.length > 1) {
        addFieldCandidate(candidates, parts[0], 0.72, 'header-company', isLikelyCompanyValue);

        for (const part of parts.slice(1)) {
          addFieldCandidate(candidates, part, 0.68, 'header-location', isLikelyLocationValue);
        }

        continue;
      }

      if ((element instanceof HTMLAnchorElement || element.querySelector('a')) && isLikelyCompanyValue(text)) {
        addFieldCandidate(candidates, text, 0.62, 'header-link-company', isLikelyCompanyValue);
      }

      addFieldCandidate(candidates, text, 0.56, 'header-short-location', isLikelyLocationValue);
    }
  }

  return candidates;
}
