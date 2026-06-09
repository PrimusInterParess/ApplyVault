export function normalizeWhitespace(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

export function normalizeLines(lines: string[]): string {
  return lines
    .map((line) => normalizeWhitespace(line))
    .filter(Boolean)
    .join('\n');
}
