import { normalizeDescription } from './description';
import { getNormalizedText } from './shared';
import type { JsonLdJobPosting } from './types';

function toArray<TValue>(value: TValue | TValue[] | undefined): TValue[] {
  if (!value) {
    return [];
  }

  return Array.isArray(value) ? value : [value];
}

function isJobPostingType(typeValue: unknown): boolean {
  const types = toArray(typeValue as string | string[]);
  return types.some((typeName) => typeof typeName === 'string' && typeName.toLowerCase() === 'jobposting');
}

function getLocationFromValue(locationValue: unknown): string | undefined {
  if (!locationValue) {
    return undefined;
  }

  if (Array.isArray(locationValue)) {
    const locations = locationValue
      .map((value) => getLocationFromValue(value))
      .filter((value, index, all): value is string => Boolean(value) && all.indexOf(value) === index);

    if (locations.length === 0) {
      return undefined;
    }

    return locations.join(' | ');
  }

  if (typeof locationValue !== 'object') {
    return getNormalizedText(String(locationValue));
  }

  const locationRecord = locationValue as {
    address?: {
      streetAddress?: string;
      addressLocality?: string;
      addressRegion?: string;
      postalCode?: string;
      addressCountry?: string | { name?: string };
    };
    name?: string;
  };

  const address = locationRecord.address;

  if (!address) {
    return getNormalizedText(locationRecord.name);
  }

  const countryName =
    typeof address.addressCountry === 'string'
      ? address.addressCountry
      : address.addressCountry?.name;

  const parts = [
    address.streetAddress,
    address.addressLocality,
    address.addressRegion,
    address.postalCode,
    countryName
  ]
    .map((part) => getNormalizedText(part))
    .filter((part): part is string => Boolean(part));

  return parts.length > 0 ? parts.join(', ') : undefined;
}

function findJobPostingNode(node: unknown, documentRef: Document): JsonLdJobPosting | undefined {
  if (!node || typeof node !== 'object') {
    return undefined;
  }

  if (Array.isArray(node)) {
    for (const item of node) {
      const foundItem = findJobPostingNode(item, documentRef);

      if (foundItem) {
        return foundItem;
      }
    }

    return undefined;
  }

  const record = node as Record<string, unknown>;

  if (isJobPostingType(record['@type'])) {
    const hiringOrganization =
      typeof record.hiringOrganization === 'object' && record.hiringOrganization
        ? (record.hiringOrganization as { name?: string })
        : undefined;

    return {
      title: getNormalizedText((record.title as string | undefined) ?? (record.name as string | undefined)),
      companyName: getNormalizedText(hiringOrganization?.name),
      location:
        getLocationFromValue(record.jobLocation) ??
        getLocationFromValue(record.applicantLocationRequirements) ??
        getLocationFromValue(record.jobLocationType),
      description: normalizeDescription(record.description as string | undefined, documentRef)
    };
  }

  if (record['@graph']) {
    return findJobPostingNode(record['@graph'], documentRef);
  }

  return undefined;
}

export function extractJsonLdJobPosting(documentRef: Document): JsonLdJobPosting | undefined {
  const scripts = Array.from(documentRef.querySelectorAll('script[type="application/ld+json"]'));

  for (const script of scripts) {
    const scriptContent = script.textContent?.trim();

    if (!scriptContent) {
      continue;
    }

    try {
      const parsedContent = JSON.parse(scriptContent) as unknown;
      const jobPosting = findJobPostingNode(parsedContent, documentRef);

      if (jobPosting) {
        return jobPosting;
      }
    } catch {
      continue;
    }
  }

  return undefined;
}
