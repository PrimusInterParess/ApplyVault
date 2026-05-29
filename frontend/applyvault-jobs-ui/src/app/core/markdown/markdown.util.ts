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

  return typeof rendered === 'string' ? rendered : '';
}

export function renderInlineMarkdown(text: string): string {
  const trimmed = text.trim();

  if (!trimmed) {
    return '';
  }

  const rendered = marked.parseInline(trimmed, markedOptions);

  return typeof rendered === 'string' ? rendered : '';
}
