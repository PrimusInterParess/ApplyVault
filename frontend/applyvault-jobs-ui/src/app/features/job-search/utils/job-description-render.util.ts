import { renderMarkdown } from '../../../core/markdown/markdown.util';

const htmlTagPattern = /<\/?[a-z][\s\S]*?>/i;
const blockHtmlTagPattern =
  /<\/?(?:p|div|section|article|ul|ol|li|h[1-6]|table|tr|td|th|thead|tbody|blockquote|br)\b/i;
const bulletLinePattern = /^([•·▪◦\-*–—]\s*|\d+[.)]\s+)(.+)$/;

export function renderJobDescription(raw: string | null | undefined): string {
  const text = raw?.trim() ?? '';

  if (!text) {
    return '';
  }

  if (htmlTagPattern.test(text)) {
    return normalizeDescriptionHtml(text);
  }

  return renderMarkdown(plainTextToMarkdown(text));
}

function normalizePlainText(text: string): string {
  const lines = text.replace(/\r\n?/g, '\n').split('\n');
  const normalizedLines: string[] = [];
  let previousWasBlank = false;

  for (const line of lines) {
    const trimmed = line.replace(/\t/g, ' ').trim();

    if (trimmed.length === 0) {
      if (!previousWasBlank && normalizedLines.length > 0) {
        normalizedLines.push('');
        previousWasBlank = true;
      }
      continue;
    }

    previousWasBlank = false;
    normalizedLines.push(trimmed);
  }

  return normalizedLines.join('\n').trim();
}

function plainTextToMarkdown(text: string): string {
  const normalized = normalizePlainText(text);
  const lines = normalized.split('\n');
  const output: string[] = [];
  let inList = false;

  for (const line of lines) {
    if (line === '') {
      if (inList) {
        inList = false;
      }
      output.push('');
      continue;
    }

    const bulletMatch = line.match(bulletLinePattern);
    if (bulletMatch) {
      if (!inList) {
        if (output.length > 0 && output[output.length - 1] !== '') {
          output.push('');
        }
        inList = true;
      }
      output.push(`- ${bulletMatch[2].trim()}`);
      continue;
    }

    inList = false;
    output.push(line);
  }

  return output.join('\n').trim();
}

function normalizeDescriptionHtml(html: string): string {
  let normalized = html
    .replace(/\r\n?/g, '\n')
    .replace(/<\/?(div|section|article|header|footer)\b[^>]*>/gi, (match) =>
      /^<\//.test(match) ? '</p>' : '<p>'
    )
    .replace(/<\/?span\b[^>]*>/gi, '')
    .replace(/<br\s*\/?>/gi, '<br>')
    .replace(/<p>\s*<\/p>/gi, '')
    .replace(/\n{3,}/g, '\n\n');

  if (!blockHtmlTagPattern.test(normalized)) {
    return renderMarkdown(plainTextToMarkdown(normalized));
  }

  return normalized.trim();
}
