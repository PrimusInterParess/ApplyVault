export type MarkdownWrapKind = 'bold' | 'italic' | 'link';

export interface MarkdownSelectionEdit {
  readonly value: string;
  readonly selectionStart: number;
  readonly selectionEnd: number;
}

export function wrapMarkdownSelection(
  value: string,
  selectionStart: number,
  selectionEnd: number,
  kind: MarkdownWrapKind
): MarkdownSelectionEdit {
  const selected = value.slice(selectionStart, selectionEnd);

  if (kind === 'link') {
    const label = selected || 'link text';
    const wrapped = `[${label}](url)`;
    const urlStart = selectionStart + label.length + 3;
    const urlEnd = urlStart + 3;

    return {
      value: value.slice(0, selectionStart) + wrapped + value.slice(selectionEnd),
      selectionStart: urlStart,
      selectionEnd: urlEnd
    };
  }

  const marker = kind === 'bold' ? '**' : '*';
  const content = selected || (kind === 'bold' ? 'bold text' : 'italic text');
  const wrapped = `${marker}${content}${marker}`;
  const start = selectionStart + marker.length;
  const end = start + content.length;

  return {
    value: value.slice(0, selectionStart) + wrapped + value.slice(selectionEnd),
    selectionStart: start,
    selectionEnd: end
  };
}

export function prefixMarkdownLine(
  value: string,
  selectionStart: number,
  selectionEnd: number,
  prefix: string
): MarkdownSelectionEdit {
  const lineStart = value.lastIndexOf('\n', Math.max(0, selectionStart - 1)) + 1;
  const lineEndRaw = value.indexOf('\n', selectionEnd);
  const lineEnd = lineEndRaw === -1 ? value.length : lineEndRaw;
  const line = value.slice(lineStart, lineEnd);

  if (line.startsWith(prefix)) {
    const nextValue = value.slice(0, lineStart) + line.slice(prefix.length) + value.slice(lineEnd);
    const delta = prefix.length;

    return {
      value: nextValue,
      selectionStart: Math.max(lineStart, selectionStart - delta),
      selectionEnd: Math.max(lineStart, selectionEnd - delta)
    };
  }

  const nextValue = value.slice(0, lineStart) + prefix + line + value.slice(lineEnd);

  return {
    value: nextValue,
    selectionStart: selectionStart + prefix.length,
    selectionEnd: selectionEnd + prefix.length
  };
}
