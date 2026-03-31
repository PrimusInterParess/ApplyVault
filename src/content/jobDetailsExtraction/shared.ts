import { normalizeWhitespace } from '../../shared/utils/textCleanup';
import {
  GENERIC_TITLE_SELECTORS,
  HIRING_MANAGER_LABELS
} from './constants';
import type { FieldCandidate } from './types';

export function getNormalizedText(value: string | null | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalizedValue = normalizeWhitespace(value);
  return normalizedValue || undefined;
}

export function getTextFromElement(element: Element | null | undefined): string | undefined {
  if (!element) {
    return undefined;
  }

  return getNormalizedText(element.textContent);
}

export function getFirstMatchingText(documentRef: Document, selectors: string[]): string | undefined {
  for (const selector of selectors) {
    const text = getTextFromElement(documentRef.querySelector(selector));

    if (text) {
      return text;
    }
  }

  return undefined;
}

export function addFieldCandidate(
  candidates: FieldCandidate[],
  value: string | undefined,
  confidence: number,
  source: string,
  predicate?: (value: string) => boolean
): void {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue) {
    return;
  }

  if (predicate && !predicate(normalizedValue)) {
    return;
  }

  candidates.push({
    value: normalizedValue,
    confidence,
    source
  });
}

export function pickBestCandidate(candidates: FieldCandidate[]): string | undefined {
  if (candidates.length === 0) {
    return undefined;
  }

  const dedupedCandidates = new Map<string, FieldCandidate>();

  for (const candidate of candidates) {
    const key = candidate.value.toLowerCase();
    const existingCandidate = dedupedCandidates.get(key);

    if (
      !existingCandidate ||
      candidate.confidence > existingCandidate.confidence ||
      (candidate.confidence === existingCandidate.confidence && candidate.value.length < existingCandidate.value.length)
    ) {
      dedupedCandidates.set(key, candidate);
    }
  }

  return Array.from(dedupedCandidates.values())
    .sort((left, right) => {
      if (right.confidence !== left.confidence) {
        return right.confidence - left.confidence;
      }

      if (left.value.length !== right.value.length) {
        return left.value.length - right.value.length;
      }

      return left.source.localeCompare(right.source);
    })[0]?.value;
}

export function getMetaContent(documentRef: Document, key: string): string | undefined {
  const metaElement = documentRef.querySelector(`meta[name="${key}"], meta[property="${key}"]`);
  return getNormalizedText(metaElement?.getAttribute('content'));
}

export function splitMetadataText(value: string): string[] {
  return value
    .split(/\s*[•|·]\s*|\s{2,}/)
    .map((part) => getNormalizedText(part))
    .filter((part): part is string => Boolean(part));
}

export function extractLabeledValue(lines: string[], labels: string[]): string | undefined {
  const normalizedLabels = labels.map((label) => label.toLowerCase());

  for (let index = 0; index < lines.length; index += 1) {
    const currentLine = lines[index];
    const normalizedLine = currentLine.toLowerCase();

    for (const label of normalizedLabels) {
      if (normalizedLine === label || normalizedLine === `${label}:`) {
        const nextLine = lines[index + 1];

        if (nextLine) {
          return nextLine;
        }
      }

      if (normalizedLine.startsWith(`${label}:`)) {
        const inlineValue = normalizeWhitespace(currentLine.slice(label.length + 1));

        if (inlineValue) {
          return inlineValue;
        }
      }
    }
  }

  return undefined;
}

export function extractLabeledValueFromElements(documentRef: Document, labels: string[]): string | undefined {
  const normalizedLabels = labels.map((label) => label.toLowerCase());
  const elements = Array.from(documentRef.querySelectorAll('p, li, div, section, article'));

  for (const element of elements) {
    const text = getTextFromElement(element);

    if (!text) {
      continue;
    }

    const normalizedText = text.toLowerCase();

    for (const label of normalizedLabels) {
      if (!normalizedText.startsWith(`${label}:`)) {
        continue;
      }

      const inlineValue = getNormalizedText(text.slice(label.length + 1));

      if (inlineValue) {
        return inlineValue;
      }
    }
  }

  return undefined;
}

export function collectSelectorTexts(documentRef: Document, selectors: string[]): string[] {
  const texts: string[] = [];
  const seenTexts = new Set<string>();

  for (const selector of selectors) {
    const elements = Array.from(documentRef.querySelectorAll(selector)).slice(0, 5);

    for (const element of elements) {
      const text = getTextFromElement(element);

      if (!text) {
        continue;
      }

      const key = text.toLowerCase();

      if (seenTexts.has(key)) {
        continue;
      }

      seenTexts.add(key);
      texts.push(text);
    }
  }

  return texts;
}

export function findSectionByHeading(documentRef: Document, headings: string[]): Element | undefined {
  const allCandidates = Array.from(documentRef.querySelectorAll('section, article, div'));
  const normalizedHeadings = headings.map((heading) => heading.toLowerCase());

  return allCandidates.find((element) => {
    const titleElement = element.querySelector('h1, h2, h3, h4, h5, h6, strong, span');
    const sectionTitle = getTextFromElement(titleElement)?.toLowerCase();

    if (!sectionTitle) {
      return false;
    }

    return normalizedHeadings.some((heading) => sectionTitle.includes(heading));
  });
}

export function collectHeaderMetadataContainers(documentRef: Document): {
  titleElement?: Element;
  containers: Element[];
} {
  const titleElement = documentRef.querySelector(GENERIC_TITLE_SELECTORS.join(', '));

  if (!titleElement) {
    return {
      containers: []
    };
  }

  const containers = [
    titleElement.parentElement,
    titleElement.closest('header, article, section, main, div'),
    titleElement.parentElement?.parentElement
  ].filter((element, index, all): element is Element => Boolean(element) && all.indexOf(element) === index);

  return {
    titleElement,
    containers
  };
}

export function createPositionSummary(
  jobDescription: string | undefined,
  preferredSummary: string | undefined
): string | undefined {
  if (preferredSummary) {
    return preferredSummary;
  }

  if (!jobDescription) {
    return undefined;
  }

  const firstSentenceMatch = jobDescription.match(/(.+?[.!?])(?:\s|$)/);
  const firstSentence = firstSentenceMatch?.[1] ?? jobDescription.slice(0, 240);
  return getNormalizedText(firstSentence);
}

export function isHiringManagerLabel(value: string): boolean {
  const normalizedValue = value.toLowerCase();
  return HIRING_MANAGER_LABELS.some(
    (label) => normalizedValue === label || normalizedValue.startsWith(`${label}:`)
  );
}
