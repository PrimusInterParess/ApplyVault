import { marked } from 'marked';

const markedOptions = {
  async: false as const,
  breaks: true,
  gfm: true
};

export function renderMarkdown(text: string): string {
  const trimmed = text.trim();

  if (!trimmed) {
    return '';
  }

  const rendered = marked.parse(trimmed, markedOptions);

  return typeof rendered === 'string' ? decorateRenderedLinks(rendered) : '';
}

export function renderInlineMarkdown(text: string): string {
  const trimmed = text.trim();

  if (!trimmed) {
    return '';
  }

  const rendered = marked.parseInline(trimmed, markedOptions);

  return typeof rendered === 'string' ? decorateRenderedLinks(rendered) : '';
}

function decorateRenderedLinks(html: string): string {
  if (typeof document === 'undefined') {
    return html;
  }

  const template = document.createElement('template');
  template.innerHTML = html;

  for (const anchor of template.content.querySelectorAll('a[href]')) {
    const href = anchor.getAttribute('href');
    anchor.setAttribute('href', normalizeExternalHref(href));
    anchor.setAttribute('target', '_blank');
    anchor.setAttribute('rel', 'noopener noreferrer');
  }

  return template.innerHTML;
}

function normalizeExternalHref(href: string | null): string {
  const trimmed = href?.trim() ?? '';

  if (!trimmed || trimmed.startsWith('#') || /^[a-z][a-z\d+.-]*:/i.test(trimmed)) {
    return trimmed;
  }

  if (trimmed.startsWith('//')) {
    return `https:${trimmed}`;
  }

  if (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
    return `mailto:${trimmed}`;
  }

  return `https://${trimmed}`;
}
