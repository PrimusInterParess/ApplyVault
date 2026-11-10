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

  const html = htmlTagPattern.test(text)
    ? normalizeDescriptionHtml(text)
    : renderMarkdown(plainTextToMarkdown(text));

  return promoteDescriptionHeadings(repairBrokenMarkdownHtml(html));
}

const strongLabelParagraphPattern =
  /<p>\s*<(strong|b)>([^<]{1,80})<\/\1>\s*(:)?\s*(?:<br\s*\/?>\s*)?<\/p>/gi;
const colonLabelParagraphPattern = /<p>([^<]{2,60}:)\s*<\/p>/gi;
const listLeadInParagraphPattern = /<p>([^<]{2,60})<\/p>(\s*)(?=<(?:ul|ol)\b)/gi;

function promoteDescriptionHeadings(html: string): string {
  return html
    .replace(strongLabelParagraphPattern, (match, _tag: string, inner: string, colon?: string) => {
      const text = `${inner.trim()}${colon ?? ''}`;
      return isSectionLabel(text) ? `<h3>${text}</h3>` : match;
    })
    .replace(colonLabelParagraphPattern, (match, inner: string) => {
      const text = inner.trim();
      return isSectionLabel(text) ? `<h3>${text}</h3>` : match;
    })
    .replace(listLeadInParagraphPattern, (match, inner: string, space: string) => {
      const text = inner.trim();
      return isSectionLabel(text) ? `<h3>${text}</h3>${space}` : match;
    });
}

function repairBrokenMarkdownHtml(html: string): string {
  return html.replace(/<p>([\s\S]*?)<\/p>/gi, (match, inner: string) => {
    if (/<[a-z]/i.test(inner)) {
      return match;
    }

    const raw = decodeHtmlEntities(inner).replace(/\s+/g, ' ').trim();
    if (!raw || isEmphasisOnly(raw)) {
      return '';
    }

    const stripped = stripEdgeMarkers(raw);
    if (!stripped) {
      return '';
    }

    if (startsWithBold(raw) && isSectionLabel(stripped)) {
      return `<h3>${escapeHtml(stripped)}</h3>`;
    }

    return `<p>${escapeHtml(stripped)}</p>`;
  });
}

function isEmphasisOnly(text: string): boolean {
  return /^[*_\s]+$/.test(text);
}

function startsWithBold(text: string): boolean {
  return /^\s*(?:_\s*)?(?:\*\*|__)/.test(text);
}

function stripEdgeMarkers(text: string): string {
  return text.replace(/^(?:[*_]+\s*)+/, '').replace(/(?:\s*[*_])+$/, '').trim();
}

function decodeHtmlEntities(text: string): string {
  return text
    .replace(/&nbsp;/gi, ' ')
    .replace(/&amp;/gi, '&')
    .replace(/&lt;/gi, '<')
    .replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"')
    .replace(/&#39;/gi, "'");
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function isSectionLabel(text: string): boolean {
  const words = text.split(/\s+/).filter((word) => word.length > 0);

  if (text.length < 2 || text.length > 60 || words.length > 8) {
    return false;
  }

  return !/[.!?]/.test(text.replace(/:$/, ''));
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
