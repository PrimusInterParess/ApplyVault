import { GitHubRepositoryListItem } from '../models/cv-project.model';

export const INSUFFICIENT_SUMMARY_DATA_MESSAGE =
  'This repository does not include enough information to generate a CV summary. Add a README or a substantive repository description on GitHub, or include topics that explain the project.';

const MIN_README_CHARS = 60;
const MIN_STANDALONE_DESCRIPTION_CHARS = 40;
const MIN_COMBINED_DESCRIPTION_CHARS = 20;

const PLACEHOLDER_EXACT_MATCHES = new Set([
  'test',
  'testing',
  'todo',
  'wip',
  'sample',
  'demo',
  'playground',
  'temp',
  'tmp',
  'hello world',
  'new repo',
  'my repo',
  'common'
]);

const PLACEHOLDER_PREFIXES = ['test', 'testing', 'todo', 'sample', 'demo', 'wip', 'temp', 'tmp', 'playground'];
const PLACEHOLDER_WORD_PATTERN = /\b(test|testing|todo|demo|sample|wip|temp|tmp|playground)\b/i;

function normalize(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function isPlaceholderText(text: string | null | undefined): boolean {
  const normalized = normalize(text);

  if (!normalized) {
    return true;
  }

  const lower = normalized.toLowerCase();

  if (PLACEHOLDER_EXACT_MATCHES.has(lower)) {
    return true;
  }

  if (normalized.length <= 15) {
    return true;
  }

  if (PLACEHOLDER_PREFIXES.some((prefix) => lower.startsWith(prefix) && normalized.length < 32)) {
    return true;
  }

  if (PLACEHOLDER_WORD_PATTERN.test(lower) && normalized.length < 36) {
    return true;
  }

  return false;
}

function isSubstantiveReadme(readmeText: string | null | undefined): boolean {
  const normalized = normalize(readmeText);
  return Boolean(normalized && normalized.length >= MIN_README_CHARS && !isPlaceholderText(normalized));
}

function isSubstantiveStandaloneDescription(description: string | null | undefined): boolean {
  const normalized = normalize(description);
  return Boolean(
    normalized &&
      normalized.length >= MIN_STANDALONE_DESCRIPTION_CHARS &&
      !isPlaceholderText(normalized)
  );
}

function isSubstantiveCombinedDescription(
  description: string | null | undefined,
  hasLanguage: boolean,
  hasTopics: boolean
): boolean {
  const normalized = normalize(description);

  if (
    !normalized ||
    normalized.length < MIN_COMBINED_DESCRIPTION_CHARS ||
    isPlaceholderText(normalized)
  ) {
    return false;
  }

  return hasLanguage || hasTopics;
}

function getMeaningfulTopics(topics: readonly string[]): readonly string[] {
  return topics
    .map((topic) => normalize(topic))
    .filter((topic): topic is string => Boolean(topic && !isPlaceholderText(topic)));
}

export function hasSufficientSummaryData(
  repo: GitHubRepositoryListItem,
  readmeText: string | null | undefined
): boolean {
  const trimmedReadme = normalize(readmeText);
  const trimmedDescription = normalize(repo.description);
  const hasLanguage = Boolean(normalize(repo.primaryLanguage));
  const meaningfulTopics = getMeaningfulTopics(repo.topics);

  if (isSubstantiveReadme(trimmedReadme)) {
    return true;
  }

  if (isSubstantiveStandaloneDescription(trimmedDescription)) {
    return true;
  }

  if (isSubstantiveCombinedDescription(trimmedDescription, hasLanguage, meaningfulTopics.length > 0)) {
    return true;
  }

  if (
    meaningfulTopics.length >= 2 &&
    (hasLanguage || isSubstantiveCombinedDescription(trimmedDescription, false, true))
  ) {
    return true;
  }

  return false;
}
