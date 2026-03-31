import {
  DESCRIPTION_BLOCK_TAGS,
  DESCRIPTION_SKIPPED_TAGS,
  SAFE_DESCRIPTION_LINK_PROTOCOLS
} from './constants';
import { getNormalizedText } from './shared';

function escapeHtmlText(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function normalizeMarkdownOutput(value: string): string {
  const lines = value
    .replace(/\r/g, '')
    .split('\n')
    .map((line) => line.replace(/[ \t]+$/g, ''));

  while (lines[0] === '') {
    lines.shift();
  }

  while (lines[lines.length - 1] === '') {
    lines.pop();
  }

  const compactedLines: string[] = [];
  let previousLineWasBlank = false;

  for (const line of lines) {
    if (!line.trim()) {
      if (!previousLineWasBlank) {
        compactedLines.push('');
      }

      previousLineWasBlank = true;
      continue;
    }

    compactedLines.push(line);
    previousLineWasBlank = false;
  }

  return compactedLines.join('\n').trim();
}

function repairSentenceSpacing(value: string): string {
  return value
    .replace(/([.!?])([A-Z])/g, '$1 $2')
    .replace(/\b(a|an|and|as|at|for|from|in|into|of|on|or|the|to|with)([A-Z][a-z])/g, '$1 $2');
}

function repairDescriptionLine(line: string): string {
  const trimmedLine = line.trim();

  if (!trimmedLine) {
    return '';
  }

  const withoutLeadingControls = trimmedLine.replace(/^[\u0000-\u001f\u007f]+/g, '').trim();

  if (!withoutLeadingControls) {
    return '';
  }

  if (/^[•▪●◦·✓✔➜➤►]/u.test(withoutLeadingControls)) {
    return `• ${withoutLeadingControls.slice(1).trim()}`;
  }

  if (/^[\u0000-\u001f\u007f]/.test(trimmedLine)) {
    return `• ${withoutLeadingControls}`;
  }

  return withoutLeadingControls.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/g, ' ');
}

function repairDescriptionFormatting(value: string): string {
  const repaired = repairSentenceSpacing(value);

  return normalizeMarkdownOutput(
    repaired
      .replace(/\r/g, '')
      .split('\n')
      .map((line) => repairDescriptionLine(line))
      .join('\n')
  );
}

function normalizePlainTextDescription(value: string): string | undefined {
  const normalized = repairDescriptionFormatting(
    value
      .replace(/\u00a0/g, ' ')
      .split('\n')
      .map((line) => (line.trim() ? getNormalizedText(line) ?? '' : ''))
      .join('\n')
  );

  return normalized || undefined;
}

function isDescriptionSkippableElement(element: Element): boolean {
  const tagName = element.tagName.toLowerCase();

  return (
    DESCRIPTION_SKIPPED_TAGS.has(tagName) ||
    element.getAttribute('aria-hidden') === 'true' ||
    element.hasAttribute('hidden')
  );
}

function isDescriptionBlockElement(element: Element): boolean {
  return DESCRIPTION_BLOCK_TAGS.has(element.tagName.toLowerCase());
}

function normalizeInlineDescription(value: string): string {
  return value.replace(/\u00a0/g, ' ').replace(/\s+/g, ' ').trim();
}

function toSafeTextLink(anchor: HTMLAnchorElement, label: string): string {
  const href = anchor.getAttribute('href')?.trim();

  if (!href) {
    return label;
  }

  try {
    const baseUrl = anchor.ownerDocument?.location.href ?? document.location.href;
    const url = new URL(href, baseUrl);

    if (!SAFE_DESCRIPTION_LINK_PROTOCOLS.has(url.protocol)) {
      return label;
    }

    return `${label} (${url.toString().replace(/>/g, '%3E')})`;
  } catch {
    return label;
  }
}

function serializeDescriptionInline(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) {
    return escapeHtmlText(normalizeInlineDescription(node.textContent ?? ''));
  }

  if (node.nodeType !== Node.ELEMENT_NODE) {
    return '';
  }

  const element = node as Element;

  if (isDescriptionSkippableElement(element)) {
    return '';
  }

  const tagName = element.tagName.toLowerCase();

  if (tagName === 'br') {
    return '\n';
  }

  if (tagName === 'code') {
    const codeText = escapeHtmlText(element.textContent?.replace(/\u00a0/g, ' ').trim() ?? '');
    return codeText;
  }

  const inlineContent = normalizeInlineDescription(
    Array.from(element.childNodes)
      .map((child) => serializeDescriptionInline(child))
      .join('')
  );

  if (!inlineContent) {
    return '';
  }

  switch (tagName) {
    case 'a':
      return element instanceof HTMLAnchorElement ? toSafeTextLink(element, inlineContent) : inlineContent;
    case 'b':
    case 'strong':
    case 'em':
    case 'i':
      return inlineContent;
    default:
      return inlineContent;
  }
}

function serializeDescriptionList(listElement: Element, ordered: boolean, depth = 0): string {
  const items = Array.from(listElement.children).filter((child): child is HTMLLIElement => child.tagName === 'LI');

  return items
    .map((item, index) => {
      const marker = ordered ? `${index + 1}) ` : '• ';
      const indent = '  '.repeat(depth);
      const nestedBlocks: string[] = [];
      const inlineParts: string[] = [];

      for (const child of Array.from(item.childNodes)) {
        if (child.nodeType === Node.ELEMENT_NODE) {
          const childElement = child as Element;
          const childTagName = childElement.tagName.toLowerCase();

          if (childTagName === 'ul' || childTagName === 'ol') {
            const nestedList = serializeDescriptionList(childElement, childTagName === 'ol', depth + 1);

            if (nestedList) {
              nestedBlocks.push(nestedList);
            }

            continue;
          }

          if (isDescriptionBlockElement(childElement) && childTagName !== 'code') {
            const nestedContent = serializeDescriptionChildren(childElement, depth + 1);

            if (nestedContent) {
              inlineParts.push(nestedContent);
            }

            continue;
          }
        }

        inlineParts.push(serializeDescriptionInline(child));
      }

      const inlineContent = normalizeInlineDescription(inlineParts.join(' '));

      if (!inlineContent && nestedBlocks.length === 0) {
        return '';
      }

      const lines = [`${indent}${marker}${inlineContent}`.trimEnd(), ...nestedBlocks];
      return lines.join('\n');
    })
    .filter(Boolean)
    .join('\n');
}

function serializeDescriptionChildren(parent: Node, listDepth = 0): string {
  return Array.from(parent.childNodes)
    .map((child) => serializeDescriptionNode(child, listDepth))
    .join('');
}

function serializeDescriptionNode(node: Node, listDepth = 0): string {
  if (node.nodeType === Node.TEXT_NODE) {
    const text = normalizeInlineDescription(node.textContent ?? '');
    return text ? `${escapeHtmlText(text)}\n\n` : '';
  }

  if (node.nodeType !== Node.ELEMENT_NODE) {
    return '';
  }

  const element = node as Element;

  if (isDescriptionSkippableElement(element)) {
    return '';
  }

  const tagName = element.tagName.toLowerCase();

  switch (tagName) {
    case 'br':
      return '\n';
    case 'h1':
    case 'h2':
    case 'h3':
    case 'h4':
    case 'h5':
    case 'h6': {
      const level = Number(tagName.slice(1));
      const heading = normalizeInlineDescription(
        Array.from(element.childNodes)
          .map((child) => serializeDescriptionInline(child))
          .join('')
      );

      return heading ? `${heading}\n\n` : '';
    }
    case 'p': {
      const paragraph = normalizeInlineDescription(
        Array.from(element.childNodes)
          .map((child) => serializeDescriptionInline(child))
          .join('')
      );

      return paragraph ? `${paragraph}\n\n` : '';
    }
    case 'ul':
      return `${serializeDescriptionList(element, false, listDepth)}\n\n`;
    case 'ol':
      return `${serializeDescriptionList(element, true, listDepth)}\n\n`;
    case 'pre': {
      const code = escapeHtmlText((element.textContent ?? '').replace(/\r/g, '').trim());
      return code ? `${code}\n\n` : '';
    }
    case 'blockquote': {
      const content = normalizeMarkdownOutput(serializeDescriptionChildren(element, listDepth));

      if (!content) {
        return '';
      }

      return `${content}\n\n`;
    }
    case 'hr':
      return '\n';
    default:
      if (isDescriptionBlockElement(element)) {
        const blockContent = normalizeMarkdownOutput(serializeDescriptionChildren(element, listDepth));
        return blockContent ? `${blockContent}\n\n` : '';
      }

      const inlineContent = normalizeInlineDescription(
        Array.from(element.childNodes)
          .map((child) => serializeDescriptionInline(child))
          .join('')
      );

      return inlineContent ? `${inlineContent}\n\n` : '';
  }
}

export function normalizeDescription(
  value: string | undefined,
  documentRef: Document = document
): string | undefined {
  if (!value) {
    return undefined;
  }

  const decodeElement = documentRef.createElement('textarea');
  decodeElement.innerHTML = value;
  const decodedValue = decodeElement.value || value;
  const containsMarkup = /<\/?[a-z][\s\S]*>/i.test(decodedValue);

  if (!containsMarkup) {
    return normalizePlainTextDescription(decodedValue);
  }

  const tempElement = documentRef.createElement('div');
  tempElement.innerHTML = decodedValue;

  return repairDescriptionFormatting(serializeDescriptionChildren(tempElement)) || undefined;
}

export function getDescriptionFromElement(element: Element | null | undefined): string | undefined {
  if (!element) {
    return undefined;
  }

  const documentRef = element.ownerDocument ?? document;
  return normalizeDescription(element.innerHTML || element.textContent || undefined, documentRef);
}

export function getFirstMatchingDescription(documentRef: Document, selectors: string[]): string | undefined {
  for (const selector of selectors) {
    const description = getDescriptionFromElement(documentRef.querySelector(selector));

    if (description) {
      return description;
    }
  }

  return undefined;
}

export function getDescriptionFromSection(section: Element | undefined): string | undefined {
  if (!section) {
    return undefined;
  }

  return getDescriptionFromElement(section);
}
