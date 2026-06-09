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

function isElementHidden(element: Element): boolean {
  const htmlElement = element as HTMLElement;

  if (htmlElement.hidden || htmlElement.getAttribute('aria-hidden') === 'true') {
    return true;
  }

  const computedStyle = window.getComputedStyle(htmlElement);
  return computedStyle.display === 'none' || computedStyle.visibility === 'hidden';
}

function isTextNodeVisible(node: Text): boolean {
  const parentElement = node.parentElement;

  if (!parentElement) {
    return false;
  }

  if (IGNORED_TAGS.has(parentElement.tagName)) {
    return false;
  }

  let currentElement: Element | null = parentElement;

  while (currentElement) {
    if (isElementHidden(currentElement)) {
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

function stripNoiseNodes(root: HTMLElement, documentRef: Document): HTMLElement {
  const clone = root.cloneNode(true) as HTMLElement;
  const hostname = documentRef.location.hostname.toLowerCase();
  const noiseSelectors = [
    ...SHARED_NOISE_SELECTORS,
    ...(Object.entries(SITE_NOISE_SELECTORS).find(([domain]) => hostname.includes(domain))?.[1] ?? [])
  ];

  for (const selector of noiseSelectors) {
    for (const element of Array.from(clone.querySelectorAll(selector))) {
      element.remove();
    }
  }

  return clone;
}

function getNormalizedRenderedText(root: HTMLElement): string {
  return normalizeLines((root.innerText || '').replace(/\r/g, '').split('\n'));
}

function getNormalizedTreeWalkerText(documentRef: Document, root: HTMLElement): string {
  const walker = documentRef.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const collectedLines: string[] = [];
  let currentNode = walker.nextNode();

  while (currentNode) {
    const textNode = currentNode as Text;

    if (isTextNodeVisible(textNode)) {
      const value = textNode.textContent ?? '';

      if (value.trim()) {
        collectedLines.push(value);
      }
    }

    currentNode = walker.nextNode();
  }

  return normalizeLines(collectedLines);
}

function pickBestPageText(renderedText: string, treeWalkerText: string): string {
  if (!renderedText) {
    return treeWalkerText;
  }

  if (!treeWalkerText) {
    return renderedText;
  }

  const renderedScore = renderedText.length + renderedText.split('\n').length * 24;
  const treeWalkerScore = treeWalkerText.length + treeWalkerText.split('\n').length * 24;

  return renderedScore >= treeWalkerScore ? renderedText : treeWalkerText;
}

export function extractPageText(documentRef: Document = document): PageTextExtraction {
  if (!documentRef.body) {
    throw new Error('This page does not have a readable document body.');
  }

  const jobRoot = stripNoiseNodes(resolveJobRoot(documentRef), documentRef);
  const text = pickBestPageText(
    getNormalizedRenderedText(jobRoot),
    getNormalizedTreeWalkerText(documentRef, jobRoot)
  );

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
