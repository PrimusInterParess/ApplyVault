/** Stored as markdown `[label](url)`. Export already draws these as plain-text anchors. */

export const CV_INLINE_LINK_EVENT = 'cv-inline-link';

export interface CvInlineLinkSpan {
  start: number;
  end: number;
  label: string;
  href: string;
}

export interface CvInlineLinkSelection {
  field: HTMLElement;
  markdown: string;
  start: number;
  end: number;
  link: CvInlineLinkSpan | null;
  rect: DOMRect;
}

interface InlinePart {
  text: string;
  href?: string;
}

export function sanitizeLinkUrl(url: string): string | null {
  const trimmed = url.trim();

  if (!trimmed || /\s/.test(trimmed)) {
    return null;
  }

  let normalized = trimmed;

  if (normalized.startsWith('//')) {
    normalized = `https:${normalized}`;
  } else if (isEmailAddress(normalized)) {
    normalized = `mailto:${normalized}`;
  } else if (!/^[a-z][a-z0-9+.-]*:/i.test(normalized)) {
    normalized = `https://${normalized}`;
  }

  let uri: URL;

  try {
    uri = new URL(normalized);
  } catch {
    return null;
  }

  if (uri.protocol !== 'http:' && uri.protocol !== 'https:' && uri.protocol !== 'mailto:') {
    return null;
  }

  return uri.toString();
}

export function renderInlineField(root: HTMLElement, markdown: string): void {
  const fragment = document.createDocumentFragment();

  for (const part of parseInlineParts(markdown)) {
    if (part.href) {
      const anchor = document.createElement('a');
      anchor.className = 'cv-quiet-link';
      anchor.setAttribute('href', part.href);
      anchor.setAttribute('target', '_blank');
      anchor.setAttribute('rel', 'noopener noreferrer');
      anchor.setAttribute('title', part.href);
      anchor.style.color = 'inherit';
      anchor.style.textDecoration = 'none';
      anchor.style.cursor = 'text';
      anchor.textContent = part.text;
      fragment.append(anchor);
      continue;
    }

    if (part.text.length > 0) {
      fragment.append(document.createTextNode(part.text));
    }
  }

  root.replaceChildren(fragment);
}

export function serializeInlineField(root: HTMLElement): string {
  let value = '';

  const walk = (node: Node): void => {
    if (node.nodeType === Node.TEXT_NODE) {
      value += node.textContent ?? '';
      return;
    }

    if (!(node instanceof HTMLElement)) {
      return;
    }

    if (node.tagName === 'A') {
      const label = node.textContent ?? '';
      const href = node.getAttribute('href') ?? '';
      const storedHref = displayHref(href.trim());

      if (label.length > 0 && storedHref) {
        value += `[${label}](${storedHref})`;
        return;
      }

      value += label;
      return;
    }

    node.childNodes.forEach(walk);
  };

  root.childNodes.forEach(walk);
  return value;
}

export function captureInlineLinkSelection(): CvInlineLinkSelection | null {
  const selection = document.getSelection();

  if (!selection || selection.isCollapsed || selection.rangeCount === 0) {
    return null;
  }

  const range = selection.getRangeAt(0);
  const field = editableField(range);

  if (!field) {
    return null;
  }

  const kind = selectionKind(field, range);

  if (kind === 'mixed') {
    return null;
  }

  if (
    kind === 'plain' &&
    (range.startContainer.nodeType !== Node.TEXT_NODE || range.endContainer.nodeType !== Node.TEXT_NODE)
  ) {
    return null;
  }

  const located = locateSelection(field, range);

  if (!located) {
    return null;
  }

  const markdown = serializeInlineField(field);
  const selected = markdown.slice(located.start, located.end);

  if (kind === 'plain' && selected.trim().length === 0) {
    return null;
  }

  if (kind === 'inside' && !located.link) {
    return null;
  }

  return {
    field,
    markdown,
    start: located.start,
    end: located.end,
    link: kind === 'inside' ? located.link : null,
    rect: range.getBoundingClientRect()
  };
}

export function wrapInlineLink(markdown: string, start: number, end: number, rawUrl: string): string | null {
  const label = markdown.slice(start, end);
  const href = sanitizeLinkUrl(rawUrl);

  if (!href || label.trim().length === 0 || label.includes('[') || label.includes(']')) {
    return null;
  }

  return `${markdown.slice(0, start)}[${label}](${href})${markdown.slice(end)}`;
}

export function replaceInlineLink(markdown: string, link: CvInlineLinkSpan, rawUrl: string): string | null {
  const href = sanitizeLinkUrl(rawUrl);

  if (!href) {
    return null;
  }

  return `${markdown.slice(0, link.start)}[${link.label}](${href})${markdown.slice(link.end)}`;
}

export function removeInlineLink(markdown: string, link: CvInlineLinkSpan): string {
  return `${markdown.slice(0, link.start)}${link.label}${markdown.slice(link.end)}`;
}

function parseInlineParts(text: string): InlinePart[] {
  const parts: InlinePart[] = [];
  let index = 0;
  let plainStart = 0;

  const pushPlain = (end: number): void => {
    if (end > plainStart) {
      parts.push({ text: text.slice(plainStart, end) });
    }
  };

  while (index < text.length) {
    if (text[index] !== '[') {
      index += 1;
      continue;
    }

    const parsed = tryParseLink(text, index);

    if (!parsed) {
      index += 1;
      continue;
    }

    pushPlain(index);
    parts.push({ text: parsed.label, href: parsed.href });
    index += parsed.length;
    plainStart = index;
  }

  pushPlain(text.length);
  return parts;
}

function tryParseLink(
  text: string,
  index: number
): { label: string; href: string; length: number } | null {
  const labelEnd = text.indexOf(']', index + 1);

  if (labelEnd < 0 || labelEnd + 1 >= text.length || text[labelEnd + 1] !== '(') {
    return null;
  }

  const urlEnd = text.indexOf(')', labelEnd + 2);

  if (urlEnd < 0) {
    return null;
  }

  const label = text.slice(index + 1, labelEnd);
  const rawUrl = text.slice(labelEnd + 2, urlEnd).trim();
  const href = displayHref(rawUrl);

  if (!label || !href) {
    return null;
  }

  return { label, href, length: urlEnd - index + 1 };
}

function displayHref(rawUrl: string): string | null {
  if (!sanitizeLinkUrl(rawUrl)) {
    return null;
  }

  if (/^(https?:|mailto:)/i.test(rawUrl)) {
    return rawUrl;
  }

  return sanitizeLinkUrl(rawUrl);
}

function editableField(range: Range): HTMLElement | null {
  const start = fieldFromNode(range.startContainer);
  const end = fieldFromNode(range.endContainer);

  if (
    !start ||
    start !== end ||
    start.getAttribute('contenteditable') !== 'true' ||
    start.getAttribute('data-cv-inline-links') === 'false'
  ) {
    return null;
  }

  return start;
}

function fieldFromNode(node: Node): HTMLElement | null {
  const element = node instanceof Element ? node : node.parentElement;
  const field = element?.closest('.cv-export-preview__editable');

  return field instanceof HTMLElement ? field : null;
}

function selectionKind(field: HTMLElement, range: Range): 'plain' | 'inside' | 'mixed' {
  const startAnchor = anchorWithin(field, range.startContainer);
  const endAnchor = anchorWithin(field, range.endContainer);

  if (startAnchor && startAnchor === endAnchor) {
    return 'inside';
  }

  if (startAnchor || endAnchor || range.cloneContents().querySelector('a')) {
    return 'mixed';
  }

  return 'plain';
}

function anchorWithin(field: HTMLElement, node: Node): HTMLAnchorElement | null {
  const element = node instanceof Element ? node : node.parentElement;
  const anchor = element?.closest('a');

  if (!(anchor instanceof HTMLAnchorElement) || !field.contains(anchor)) {
    return null;
  }

  return anchor;
}

function locateSelection(
  field: HTMLElement,
  range: Range
): { start: number; end: number; link: CvInlineLinkSpan | null } | null {
  const state: {
    index: number;
    start: number | null;
    end: number | null;
    link: CvInlineLinkSpan | null;
  } = { index: 0, start: null, end: null, link: null };

  const walk = (node: Node): void => {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.textContent ?? '';

      if (range.startContainer === node) {
        state.start = state.index + range.startOffset;
      }

      if (range.endContainer === node) {
        state.end = state.index + range.endOffset;
      }

      state.index += text.length;
      return;
    }

    if (!(node instanceof HTMLElement)) {
      return;
    }

    if (node.tagName === 'A') {
      const label = node.textContent ?? '';
      const href = node.getAttribute('href') ?? '';

      if (label.length > 0 && href.length > 0) {
        const linkStart = state.index;
        state.index += 1;
        node.childNodes.forEach(walk);
        state.index += href.length + 3;
        const span: CvInlineLinkSpan = { start: linkStart, end: state.index, label, href };

        if (node.contains(range.startContainer) && node.contains(range.endContainer)) {
          state.link = span;
        }

        return;
      }
    }

    node.childNodes.forEach(walk);
  };

  field.childNodes.forEach(walk);

  const start = state.start;
  const end = state.end;
  const link = state.link;

  if (start === null || end === null || start === end) {
    if (!link) {
      return null;
    }

    return { start: link.start, end: link.end, link };
  }

  if (end < start) {
    return { start: end, end: start, link };
  }

  return { start, end, link };
}

function isEmailAddress(value: string): boolean {
  return (
    !value.includes(':') &&
    !value.includes('://') &&
    !/\s/.test(value) &&
    value.split('@').length === 2 &&
    value.includes('.')
  );
}
