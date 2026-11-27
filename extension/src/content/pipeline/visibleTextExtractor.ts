import { normalizeLines } from '../../shared/utils/textCleanup';
import { GENERIC_TITLE_SELECTORS } from '../sites/generic.constants';

const IGNORED_TAGS = new Set([
  'SCRIPT',
  'STYLE',
  'NOSCRIPT',
  'SVG',
  'CANVAS',
  'IMG',
  'VIDEO',
  'AUDIO',
  'IFRAME'
]);

const SHARED_NOISE_SELECTORS = [
  'nav',
  'footer',
  '[role="navigation"]',
  '[role="contentinfo"]',
  '[class*="cookie"]',
  '[id*="cookie"]',
  '[class*="banner"]',
  '[aria-label*="cookie" i]'
];

const SITE_NOISE_SELECTORS: Record<string, string[]> = {
  'linkedin.com': ['[class*="similar-jobs"]', '[class*="jobs-you-might"]']
};

export interface PageTextExtraction {
  title: string;
  url: string;
  text: string;
  textLength: number;
}

export function extractPageText(documentRef: Document = document): PageTextExtraction {
  if (!documentRef.body) {
    throw new Error('This page does not have a readable document body.');
  }

  // Read the live DOM so visibility and ancestor styles remain meaningful.
  // Detached innerText can become textContent and include scripts and hidden templates.
  const text = getNormalizedTreeWalkerText(documentRef, resolveJobRoot(documentRef));

  if (!text) {
    throw new Error('No visible text was found on this page.');
  }

  return {
    title: documentRef.title || 'Untitled page',
    url: documentRef.location.href,
    text,
    textLength: text.length
  };
}

export function hasExtractionSignals(documentRef: Document): boolean {
  if (documentRef.querySelector('script[type="application/ld+json"]')) {
    return true;
  }

  if (documentRef.querySelector(GENERIC_TITLE_SELECTORS.join(', '))) {
    return true;
  }

  return Boolean(documentRef.querySelector('[class*="description"], [itemprop="description"], article'));
}

function isElementHidden(element: Element): boolean {
  if (element.hasAttribute('hidden') || element.getAttribute('aria-hidden') === 'true') {
    return true;
  }

  const computedStyle = element.ownerDocument.defaultView?.getComputedStyle(element);
  return computedStyle?.display === 'none' || computedStyle?.visibility === 'hidden';
}

function isTextNodeVisible(node: Node, noiseSelector: string): boolean {
  const parentElement = node.parentElement;

  if (!parentElement) {
    return false;
  }

  let currentElement: Element | null = parentElement;

  while (currentElement) {
    if (
      IGNORED_TAGS.has(currentElement.tagName) ||
      currentElement.matches(noiseSelector) ||
      isElementHidden(currentElement)
    ) {
      return false;
    }

    currentElement = currentElement.parentElement;
  }

  return true;
}

function resolveJobRoot(documentRef: Document): HTMLElement {
  const hostname = documentRef.location.hostname.toLowerCase();
  const siteSpecificRoot =
    hostname.includes('linkedin.com')
      ? documentRef.querySelector<HTMLElement>('.jobs-search__job-details, main')
      : documentRef.querySelector<HTMLElement>('[class*="job-details"], [class*="posting"]');

  return (
    siteSpecificRoot ??
    documentRef.querySelector<HTMLElement>('main') ??
    documentRef.querySelector<HTMLElement>('[role="main"]') ??
    documentRef.body
  );
}

function getNoiseSelector(documentRef: Document): string {
  const hostname = documentRef.location.hostname.toLowerCase();
  return [
    ...SHARED_NOISE_SELECTORS,
    ...(Object.entries(SITE_NOISE_SELECTORS).find(([domain]) => hostname.includes(domain))?.[1] ?? [])
  ].join(', ');
}

function getNormalizedTreeWalkerText(documentRef: Document, root: HTMLElement): string {
  const walker = documentRef.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const noiseSelector = getNoiseSelector(documentRef);
  const collectedLines: string[] = [];
  let currentNode = walker.nextNode();

  while (currentNode) {
    if (isTextNodeVisible(currentNode, noiseSelector)) {
      const value = currentNode.textContent ?? '';

      if (value.trim()) {
        collectedLines.push(value);
      }
    }

    currentNode = walker.nextNode();
  }

  return normalizeLines(collectedLines);
}
