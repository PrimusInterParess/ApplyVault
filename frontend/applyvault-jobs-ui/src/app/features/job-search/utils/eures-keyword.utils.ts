export function canonicalizeEuresKeyword(keyword: string): string {
  const trimmedKeyword = keyword.trim();

  if (trimmedKeyword.length === 0) {
    return '';
  }

  const normalizedKeyword = trimmedKeyword.toLowerCase();
  if (normalizedKeyword === '.net' || normalizedKeyword === 'dotnet') {
    return '.NET';
  }

  return trimmedKeyword;
}

export function matchesEuresKeyword(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
}

export function normalizeEuresKeywords(keywords: readonly string[]): string[] {
  const normalizedKeywords: string[] = [];

  for (const keyword of keywords) {
    const normalizedKeyword = canonicalizeEuresKeyword(keyword);

    if (
      normalizedKeyword.length === 0 ||
      normalizedKeywords.some((existingKeyword) =>
        matchesEuresKeyword(existingKeyword, normalizedKeyword)
      )
    ) {
      continue;
    }

    normalizedKeywords.push(normalizedKeyword);
  }

  return normalizedKeywords;
}
