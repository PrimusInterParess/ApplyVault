import type { ScrapeResult } from '../shared/models/scrapeResult';
import { normalizeLines } from '../shared/utils/textCleanup';
import { extractJobDetails } from './extractJobDetails';

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

export function extractVisibleText(documentRef: Document = document): ScrapeResult {
  const root = documentRef.body;

  if (!root) {
    throw new Error('This page does not have a readable document body.');
  }

  const text = pickBestPageText(getNormalizedRenderedText(root), getNormalizedTreeWalkerText(documentRef, root));

  if (!text) {
    throw new Error('No visible text was found on this page.');
  }

  return {
    title: documentRef.title || 'Untitled page',
    url: documentRef.location.href,
    text,
    textLength: text.length,
    extractedAt: new Date().toISOString(),
    jobDetails: extractJobDetails(documentRef, text)
  };
}
