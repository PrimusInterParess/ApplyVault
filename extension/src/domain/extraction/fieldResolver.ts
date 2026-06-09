import type { DetectedPageType, JobDetails } from '../../shared/models/scrapeResult';
import { createPositionSummary } from '../../content/jobDetailsExtraction/shared';
import type { ContactsExtractionResult, ExtractionContext, FieldExtraction, JobField, ResolvedJobDetails } from './types';
import { EXTRACTOR_VERSION } from './types';

const SOURCE_PRIORITY: Record<string, number> = {
  'json-ld': 100,
  'linkedin-job': 95,
  'linkedin-feed': 90,
  generic: 80,
  'meta-tag': 70,
  contacts: 65,
  'teamtailor-signal': 10
};

function getSourcePriority(source: string): number {
  return SOURCE_PRIORITY[source] ?? 50;
}

function pickBestFieldValue(candidates: FieldExtraction[]): { value?: string; source?: string } {
  if (candidates.length === 0) {
    return {};
  }

  const deduped = new Map<string, FieldExtraction>();

  for (const candidate of candidates) {
    const key = candidate.value.toLowerCase();
    const existing = deduped.get(key);

    if (!existing) {
      deduped.set(key, candidate);
      continue;
    }

    const existingScore = existing.confidence + getSourcePriority(existing.source) / 100;
    const candidateScore = candidate.confidence + getSourcePriority(candidate.source) / 100;

    if (candidateScore > existingScore) {
      deduped.set(key, candidate);
    }
  }

  const best = Array.from(deduped.values()).sort((left, right) => {
    const leftScore = left.confidence + getSourcePriority(left.source) / 100;
    const rightScore = right.confidence + getSourcePriority(right.source) / 100;

    if (rightScore !== leftScore) {
      return rightScore - leftScore;
    }

    return left.value.length - right.value.length;
  })[0];

  return {
    value: best?.value,
    source: best?.source
  };
}

function resolveField(
  candidates: FieldExtraction[],
  field: JobField,
  fieldSources: Partial<Record<JobField, string>>
): string | undefined {
  const fieldCandidates = candidates.filter((candidate) => candidate.field === field);
  const { value, source } = pickBestFieldValue(fieldCandidates);

  if (value && source) {
    fieldSources[field] = source;
  }

  return value;
}

export function field(value: string | undefined, fieldName: JobField, confidence: number, source: string): FieldExtraction[] {
  if (!value?.trim()) {
    return [];
  }

  return [{ field: fieldName, value: value.trim(), confidence, source }];
}

export function resolveJobDetails(
  ctx: ExtractionContext,
  candidates: FieldExtraction[],
  contacts: ContactsExtractionResult,
  metaDescription?: string
): ResolvedJobDetails {
  const fieldSources: Partial<Record<JobField, string>> = {};
  const jobDescription = resolveField(candidates, 'jobDescription', fieldSources);
  const positionSummary =
    resolveField(candidates, 'positionSummary', fieldSources) ??
    createPositionSummary(jobDescription, ctx.listingIsInactive ? undefined : metaDescription);

  if (positionSummary && !fieldSources.positionSummary) {
    fieldSources.positionSummary = metaDescription ? 'meta-tag' : 'derived';
  }

  const jobDetails: JobDetails = {
    sourceHostname: ctx.document.location.hostname,
    detectedPageType: ctx.pageType,
    jobTitle: resolveField(candidates, 'jobTitle', fieldSources),
    companyName: resolveField(candidates, 'companyName', fieldSources),
    location: resolveField(candidates, 'location', fieldSources),
    jobDescription,
    positionSummary,
    hiringManagerName: resolveField(candidates, 'hiringManagerName', fieldSources),
    hiringManagerContacts: contacts.hiringManagerContacts,
    fieldSources,
    extractorVersion: EXTRACTOR_VERSION
  };

  return { jobDetails, fieldSources };
}

export function mergePageType(current: DetectedPageType, incoming: DetectedPageType): DetectedPageType {
  if (current !== 'generic-page') {
    return current;
  }

  return incoming;
}
