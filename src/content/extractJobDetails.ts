import type {
  DetectedPageType,
  HiringManagerContact,
  HiringManagerContactType,
  JobDetails
} from '../shared/models/scrapeResult';
import { normalizeLines, normalizeWhitespace } from '../shared/utils/textCleanup';

interface JsonLdJobPosting {
  title?: string;
  companyName?: string;
  location?: string;
  description?: string;
}

interface FieldCandidate {
  value: string;
  confidence: number;
  source: string;
}

const LINKEDIN_JOB_TITLE_SELECTORS = [
  '.job-details-jobs-unified-top-card__job-title',
  '.jobs-unified-top-card__job-title',
  '.top-card-layout__title',
  '.topcard__title',
  'main h1',
  'h1'
];

const LINKEDIN_COMPANY_SELECTORS = [
  '.job-details-jobs-unified-top-card__company-name',
  '.jobs-unified-top-card__company-name',
  '.topcard__org-name-link',
  '.topcard__flavor',
  '[data-test-job-details-company-name]',
  'a[href*="/company/"]'
];

const LINKEDIN_LOCATION_SELECTORS = [
  '.job-details-jobs-unified-top-card__bullet',
  '.jobs-unified-top-card__bullet',
  '.topcard__flavor--bullet',
  '[data-test-job-details-location]',
  '[data-test-job-location]'
];

const LINKEDIN_DESCRIPTION_SELECTORS = [
  '.jobs-description__content',
  '.jobs-box__html-content',
  '.show-more-less-html__markup',
  '[data-job-detail-container]'
];

const LINKEDIN_FEED_JOB_CARD_SELECTORS = [
  'a[href*="/jobs/view/"]',
  'a[href*="/jobs/collections/"][href*="currentJobId="]'
];

const HIRING_MANAGER_SELECTORS = [
  '.hirer-card__hirer-information a[href*="/in/"]',
  '.hirer-card__hirer-information strong',
  '.hirer-card__hirer-information h3',
  '[data-view-name="job-details-hiring-team"] a[href*="/in/"]',
  '[data-view-name="job-details-hiring-team"] strong'
];

const DESCRIPTION_LABELS = ['job description', 'about the job', 'description', 'about this role'];
const COMPANY_LABELS = ['company', 'company name', 'organization', 'employer'];
const LOCATION_LABELS = ['location', 'job location', 'based in', 'arbejdssted', 'lokation', 'arbejdsplads'];
const HIRING_MANAGER_LABELS = ['hiring manager', 'recruiter', 'contact'];
const NON_PERSON_HIRING_MANAGER_TERMS = [
  'karrieremenu',
  'careermenu',
  'linkedin',
  'jobindex',
  'indeed',
  'glassdoor',
  'workday',
  'greenhouse',
  'teamtailor',
  'smartrecruiters',
  'recruitee',
  'lever',
  'jobylon',
  'personio',
  'welcome to the jungle',
  'career',
  'careers',
  'job',
  'jobs',
  'apply',
  'ansog',
  'ansøg',
  'menu'
];
const NON_METADATA_TERMS = [
  'karrieremenu',
  'careermenu',
  'linkedin',
  'jobindex',
  'indeed',
  'glassdoor',
  'workday',
  'greenhouse',
  'teamtailor',
  'smartrecruiters',
  'recruitee',
  'lever',
  'jobylon',
  'personio',
  'menu',
  'share',
  'save',
  'search',
  'login',
  'sign in',
  'sign up',
  'cookie',
  'privacy',
  'terms'
];
const NON_PERSON_EMAIL_LOCALPART_TERMS = [
  'career',
  'careers',
  'job',
  'jobs',
  'info',
  'hello',
  'contact',
  'support',
  'team',
  'recruiting',
  'recruitment',
  'talent',
  'hr',
  'admin',
  'office',
  'mail',
  'noreply',
  'no',
  'reply',
  'menu'
];
const GENERIC_TITLE_SELECTORS = ['main h1', 'article h1', 'header h1', 'h1'];
const GENERIC_COMPANY_SELECTORS = [
  '[itemprop="hiringOrganization"]',
  '[data-test*="company"]',
  '[data-qa*="company"]',
  '[class*="company"]',
  '[class*="employer"]',
  '[class*="organization"]'
];
const GENERIC_LOCATION_SELECTORS = [
  '[itemprop="jobLocation"]',
  '[data-test*="location"]',
  '[data-qa*="location"]',
  '[class*="location"]',
  '[class*="job-location"]',
  '[class*="office"]',
  '[class*="city"]',
  'address'
];

function getNormalizedText(value: string | null | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalizedValue = normalizeWhitespace(value);
  return normalizedValue || undefined;
}

function getTextFromElement(element: Element | null | undefined): string | undefined {
  if (!element) {
    return undefined;
  }

  return getNormalizedText(element.textContent);
}

function getFirstMatchingText(documentRef: Document, selectors: string[]): string | undefined {
  for (const selector of selectors) {
    const text = getTextFromElement(documentRef.querySelector(selector));

    if (text) {
      return text;
    }
  }

  return undefined;
}

function addFieldCandidate(
  candidates: FieldCandidate[],
  value: string | undefined,
  confidence: number,
  source: string,
  predicate?: (value: string) => boolean
): void {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue) {
    return;
  }

  if (predicate && !predicate(normalizedValue)) {
    return;
  }

  candidates.push({
    value: normalizedValue,
    confidence,
    source
  });
}

function pickBestCandidate(candidates: FieldCandidate[]): string | undefined {
  if (candidates.length === 0) {
    return undefined;
  }

  const dedupedCandidates = new Map<string, FieldCandidate>();

  for (const candidate of candidates) {
    const key = candidate.value.toLowerCase();
    const existingCandidate = dedupedCandidates.get(key);

    if (
      !existingCandidate ||
      candidate.confidence > existingCandidate.confidence ||
      (candidate.confidence === existingCandidate.confidence && candidate.value.length < existingCandidate.value.length)
    ) {
      dedupedCandidates.set(key, candidate);
    }
  }

  return Array.from(dedupedCandidates.values())
    .sort((left, right) => {
      if (right.confidence !== left.confidence) {
        return right.confidence - left.confidence;
      }

      if (left.value.length !== right.value.length) {
        return left.value.length - right.value.length;
      }

      return left.source.localeCompare(right.source);
    })[0]?.value;
}

function getMetaContent(documentRef: Document, key: string): string | undefined {
  const metaElement = documentRef.querySelector(`meta[name="${key}"], meta[property="${key}"]`);
  return getNormalizedText(metaElement?.getAttribute('content'));
}

function normalizeDescription(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const decodeElement = document.createElement('textarea');
  decodeElement.innerHTML = value;
  const decodedValue = decodeElement.value || value;
  const markupCandidate = /<\/?[a-z][\s\S]*>/i.test(decodedValue) ? decodedValue : value;
  const tempElement = document.createElement('div');
  tempElement.innerHTML = markupCandidate
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<\/p>/gi, '\n')
    .replace(/<\/div>/gi, '\n')
    .replace(/<\/section>/gi, '\n')
    .replace(/<\/article>/gi, '\n')
    .replace(/<\/h[1-6]>/gi, '\n')
    .replace(/<li\b[^>]*>/gi, '\n• ')
    .replace(/<\/li>/gi, '\n');
  const lines = (tempElement.textContent || value)
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

  return normalizeLines(lines) || undefined;
}

function splitMetadataText(value: string): string[] {
  return value
    .split(/\s*[•|·]\s*|\s{2,}/)
    .map((part) => getNormalizedText(part))
    .filter((part): part is string => Boolean(part));
}

function isLikelyActionText(value: string): boolean {
  return /^(apply|ansøg|search|share|save)\b|^(deadline|tiltrædelse|start date|salary|løn|grundløn|compensation|pay|job description|description|contact|email|phone|forventninger)\b/i.test(
    value
  );
}

function isDisallowedMetadataValue(value: string): boolean {
  const normalizedValue = value.toLowerCase();

  return (
    NON_METADATA_TERMS.some((term) => normalizedValue === term || normalizedValue.includes(term)) ||
    HIRING_MANAGER_LABELS.some((label) => normalizedValue === label || normalizedValue.startsWith(`${label}:`))
  );
}

function isLikelyLocationValue(value: string): boolean {
  if (value.length < 2 || value.length > 140) {
    return false;
  }

  if (
    /@|https?:\/\//i.test(value) ||
    isLikelyActionText(value) ||
    isDisallowedMetadataValue(value) ||
    /\b(?:salary|løn|grundløn|compensation|pay|pension|bonus|benefits|forventninger)\b/i.test(value)
  ) {
    return false;
  }

  if (/\b(remote|hybrid|onsite|on-site|denmark|danmark|sweden|norway|germany|europe|emea|apac)\b/i.test(value)) {
    return true;
  }

  if (/\b\d{4,5}\b/.test(value)) {
    return true;
  }

  if (/,/.test(value)) {
    return true;
  }

  if (/\b(?:street|st\.|road|rd\.|avenue|ave\.|vej|gade|plads|all[eé]|boulevard|blvd)\b/i.test(value)) {
    return true;
  }

  const words = value.split(/\s+/);

  if (words.length === 1) {
    return /^[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+$/.test(words[0]);
  }

  if (words.length > 4 || !/^[A-Za-zÀ-ÿ0-9 .,'()/-]+$/.test(value)) {
    return false;
  }

  const normalizedWords = words
    .map((word) => word.replace(/^[^A-Za-zÀ-ÿ0-9]+|[^A-Za-zÀ-ÿ0-9]+$/g, ''))
    .filter(Boolean);

  if (normalizedWords.length === 0) {
    return false;
  }

  const capitalizedOrUppercaseWords = normalizedWords.filter((word) =>
    /^[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]*$/.test(word) || /^[A-Z]{2,}$/.test(word)
  ).length;

  return capitalizedOrUppercaseWords === normalizedWords.length;
}

function isLikelyCompanyValue(value: string): boolean {
  if (value.length < 2 || value.length > 100) {
    return false;
  }

  if (/@|https?:\/\//i.test(value) || isLikelyActionText(value) || isDisallowedMetadataValue(value)) {
    return false;
  }

  if (/^\d+$/.test(value)) {
    return false;
  }

  if (isLikelyLocationValue(value) && /\d/.test(value)) {
    return false;
  }

  return value.split(/\s+/).length <= 8;
}

function isLikelyPersonName(value: string): boolean {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue || normalizedValue.length < 3 || normalizedValue.length > 80) {
    return false;
  }

  const normalizedLower = normalizedValue.toLowerCase();

  if (
    /@|https?:\/\/|\d/.test(normalizedValue) ||
    NON_PERSON_HIRING_MANAGER_TERMS.some((term) => normalizedLower.includes(term)) ||
    HIRING_MANAGER_LABELS.some((label) => normalizedLower === label || normalizedLower.startsWith(`${label}:`))
  ) {
    return false;
  }

  const words = normalizedValue
    .replace(/[(),]/g, ' ')
    .split(/\s+/)
    .filter(Boolean);

  if (words.length < 2 || words.length > 5) {
    return false;
  }

  const capitalizedWordCount = words.filter((word) => /^[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+$/.test(word)).length;

  if (capitalizedWordCount < 2) {
    return false;
  }

  return true;
}

function getEmailLocalPartTokens(email: string): string[] {
  const [localPart] = email.toLowerCase().split('@');

  return localPart
    .split(/[._-]+/)
    .map((token) => token.trim())
    .filter(
      (token) =>
        token.length >= 2 && /^[a-zà-ÿ]+$/i.test(token) && !NON_PERSON_EMAIL_LOCALPART_TERMS.includes(token)
    );
}

function getNameTokens(value: string): string[] {
  return value
    .toLowerCase()
    .split(/[^a-zà-ÿ'-]+/i)
    .map((token) => token.trim())
    .filter(Boolean);
}

function extractNameFromEmailAddress(email: string): string | undefined {
  const tokens = getEmailLocalPartTokens(email);

  if (tokens.length < 2 || tokens.length > 4) {
    return undefined;
  }

  const formattedName = tokens.map((token) => `${token.charAt(0).toUpperCase()}${token.slice(1)}`).join(' ');

  return isLikelyPersonName(formattedName) ? formattedName : undefined;
}

function scorePersonNameAgainstEmail(name: string, email: string): number {
  const emailTokens = getEmailLocalPartTokens(email);

  if (emailTokens.length === 0) {
    return 0;
  }

  const nameTokens = new Set(getNameTokens(name));
  let overlapCount = 0;

  for (const token of emailTokens) {
    if (nameTokens.has(token)) {
      overlapCount += 1;
    }
  }

  return overlapCount / emailTokens.length;
}

function getTextWindowsAroundValue(text: string, value: string, radius = 160): string[] {
  const windows: string[] = [];
  const normalizedText = text.toLowerCase();
  const normalizedValue = value.toLowerCase();
  let startIndex = 0;

  while (windows.length < 3) {
    const matchIndex = normalizedText.indexOf(normalizedValue, startIndex);

    if (matchIndex === -1) {
      break;
    }

    const sliceStart = Math.max(0, matchIndex - radius);
    const sliceEnd = Math.min(text.length, matchIndex + value.length + radius);
    windows.push(text.slice(sliceStart, sliceEnd));
    startIndex = matchIndex + normalizedValue.length;
  }

  return windows;
}

function extractBestPersonNameFromContext(contextText: string, email: string): string | undefined {
  const emailIndex = contextText.toLowerCase().indexOf(email.toLowerCase());

  if (emailIndex === -1) {
    return undefined;
  }

  const candidatePattern = /\b([A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+(?:\s+[A-ZÀ-Ý][A-Za-zÀ-ÿ'’-]+){1,3})\b/g;
  let bestCandidate: FieldCandidate | undefined;

  for (const match of contextText.matchAll(candidatePattern)) {
    const candidateValue = getNormalizedText(match[1]);
    const candidateIndex = match.index ?? -1;

    if (!candidateValue || candidateIndex === -1 || !isLikelyPersonName(candidateValue)) {
      continue;
    }

    const overlapScore = scorePersonNameAgainstEmail(candidateValue, email);
    const distancePenalty = Math.abs(emailIndex - candidateIndex) / 400;
    const nearbyContext = contextText.slice(
      Math.max(0, candidateIndex - 40),
      Math.min(contextText.length, candidateIndex + candidateValue.length + 40)
    );
    const roleBonus = /\b(contact|recruiter|manager|head|director|lead|talent|spørgsmål|kontakt)\b/i.test(
      nearbyContext
    )
      ? 0.2
      : 0;
    const confidence = overlapScore + roleBonus - distancePenalty;

    if (confidence <= 0.2) {
      continue;
    }

    if (!bestCandidate || confidence > bestCandidate.confidence) {
      bestCandidate = {
        value: candidateValue,
        confidence,
        source: 'contact-context'
      };
    }
  }

  return bestCandidate?.value;
}

function getContextTextsForEmail(documentRef: Document, email: string): string[] {
  const contexts: string[] = [];
  const seenContexts = new Set<string>();

  for (const anchor of Array.from(documentRef.querySelectorAll<HTMLAnchorElement>('a[href^="mailto:"]'))) {
    const anchorEmail = getMailtoEmailAddress(anchor.getAttribute('href') || '');

    if (!anchorEmail || anchorEmail.toLowerCase() !== email.toLowerCase()) {
      continue;
    }

    const contextualElements = [
      anchor.closest('p, li, div, section, article'),
      anchor.parentElement,
      anchor.parentElement?.parentElement
    ].filter((element, index, all): element is Element => Boolean(element) && all.indexOf(element) === index);

    for (const element of contextualElements) {
      const text = getTextFromElement(element);

      if (!text) {
        continue;
      }

      const key = text.toLowerCase();

      if (seenContexts.has(key)) {
        continue;
      }

      seenContexts.add(key);
      contexts.push(text);
    }
  }

  return contexts;
}

function collectSelectorTexts(documentRef: Document, selectors: string[]): string[] {
  const texts: string[] = [];
  const seenTexts = new Set<string>();

  for (const selector of selectors) {
    const elements = Array.from(documentRef.querySelectorAll(selector)).slice(0, 5);

    for (const element of elements) {
      const text = getTextFromElement(element);

      if (!text) {
        continue;
      }

      const key = text.toLowerCase();

      if (seenTexts.has(key)) {
        continue;
      }

      seenTexts.add(key);
      texts.push(text);
    }
  }

  return texts;
}

function getMailtoEmailAddress(href: string): string | undefined {
  const mailtoValue = href.replace(/^mailto:/i, '').trim();

  if (!mailtoValue) {
    return undefined;
  }

  const [addressPart] = mailtoValue.split('?');
  const normalizedAddress = getNormalizedText(addressPart);

  if (!normalizedAddress || !/^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i.test(normalizedAddress)) {
    return undefined;
  }

  return normalizedAddress;
}

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
  if (!locationValue || typeof locationValue !== 'object') {
    return undefined;
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

function findJobPostingNode(node: unknown): JsonLdJobPosting | undefined {
  if (!node || typeof node !== 'object') {
    return undefined;
  }

  if (Array.isArray(node)) {
    for (const item of node) {
      const foundItem = findJobPostingNode(item);

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
      title: getNormalizedText(record.title as string | undefined),
      companyName: getNormalizedText(hiringOrganization?.name),
      location:
        getLocationFromValue(record.jobLocation) ??
        getLocationFromValue(record.applicantLocationRequirements) ??
        getLocationFromValue(record.jobLocationType),
      description: normalizeDescription(record.description as string | undefined)
    };
  }

  if (record['@graph']) {
    return findJobPostingNode(record['@graph']);
  }

  return undefined;
}

function extractJsonLdJobPosting(documentRef: Document): JsonLdJobPosting | undefined {
  const scripts = Array.from(documentRef.querySelectorAll('script[type="application/ld+json"]'));

  for (const script of scripts) {
    const scriptContent = script.textContent?.trim();

    if (!scriptContent) {
      continue;
    }

    try {
      const parsedContent = JSON.parse(scriptContent) as unknown;
      const jobPosting = findJobPostingNode(parsedContent);

      if (jobPosting) {
        return jobPosting;
      }
    } catch {
      continue;
    }
  }

  return undefined;
}

function extractLabeledValue(lines: string[], labels: string[]): string | undefined {
  const normalizedLabels = labels.map((label) => label.toLowerCase());

  for (let index = 0; index < lines.length; index += 1) {
    const currentLine = lines[index];
    const normalizedLine = currentLine.toLowerCase();

    for (const label of normalizedLabels) {
      if (normalizedLine === label || normalizedLine === `${label}:`) {
        const nextLine = lines[index + 1];

        if (nextLine) {
          return nextLine;
        }
      }

      if (normalizedLine.startsWith(`${label}:`)) {
        const inlineValue = normalizeWhitespace(currentLine.slice(label.length + 1));

        if (inlineValue) {
          return inlineValue;
        }
      }
    }
  }

  return undefined;
}

function extractLabeledValueFromElements(documentRef: Document, labels: string[]): string | undefined {
  const normalizedLabels = labels.map((label) => label.toLowerCase());
  const elements = Array.from(documentRef.querySelectorAll('p, li, div, section, article'));

  for (const element of elements) {
    const text = getTextFromElement(element);

    if (!text) {
      continue;
    }

    const normalizedText = text.toLowerCase();

    for (const label of normalizedLabels) {
      if (!normalizedText.startsWith(`${label}:`)) {
        continue;
      }

      const inlineValue = getNormalizedText(text.slice(label.length + 1));

      if (inlineValue) {
        return inlineValue;
      }
    }
  }

  return undefined;
}

function collectHeaderMetadataCandidates(documentRef: Document, titleValue: string | undefined): FieldCandidate[] {
  const titleElement = documentRef.querySelector(GENERIC_TITLE_SELECTORS.join(', '));

  if (!titleElement) {
    return [];
  }

  const candidateContainers = [
    titleElement.parentElement,
    titleElement.closest('header, article, section, main, div'),
    titleElement.parentElement?.parentElement
  ].filter((element, index, all): element is Element => Boolean(element) && all.indexOf(element) === index);

  const candidates: FieldCandidate[] = [];
  const seenTexts = new Set<string>();

  for (const container of candidateContainers) {
    const elements = Array.from(container.querySelectorAll('p, span, a, li, strong, div')).slice(0, 40);

    for (const element of elements) {
      if (element === titleElement || element.querySelector('h1, h2')) {
        continue;
      }

      const text = getTextFromElement(element);

      if (!text || text === titleValue || text.length > 140) {
        continue;
      }

      const textKey = text.toLowerCase();

      if (seenTexts.has(textKey)) {
        continue;
      }

      seenTexts.add(textKey);

      const parts = splitMetadataText(text);

      if (parts.length > 1) {
        addFieldCandidate(candidates, parts[0], 0.72, 'header-company', isLikelyCompanyValue);

        for (const part of parts.slice(1)) {
          addFieldCandidate(candidates, part, 0.68, 'header-location', isLikelyLocationValue);
        }

        continue;
      }

      if ((element instanceof HTMLAnchorElement || element.querySelector('a')) && isLikelyCompanyValue(text)) {
        addFieldCandidate(candidates, text, 0.62, 'header-link-company', isLikelyCompanyValue);
      }

      addFieldCandidate(candidates, text, 0.56, 'header-short-location', isLikelyLocationValue);
    }
  }

  return candidates;
}

function findSectionByHeading(documentRef: Document, headings: string[]): Element | undefined {
  const allCandidates = Array.from(documentRef.querySelectorAll('section, article, div'));
  const normalizedHeadings = headings.map((heading) => heading.toLowerCase());

  return allCandidates.find((element) => {
    const titleElement = element.querySelector('h1, h2, h3, h4, h5, h6, strong, span');
    const sectionTitle = getTextFromElement(titleElement)?.toLowerCase();

    if (!sectionTitle) {
      return false;
    }

    return normalizedHeadings.some((heading) => sectionTitle.includes(heading));
  });
}

function getDescriptionFromSection(section: Element | undefined): string | undefined {
  if (!section) {
    return undefined;
  }

  const lines = (section.textContent || '')
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

  return normalizeLines(lines) || undefined;
}

function createPositionSummary(
  jobDescription: string | undefined,
  preferredSummary: string | undefined
): string | undefined {
  if (preferredSummary) {
    return preferredSummary;
  }

  if (!jobDescription) {
    return undefined;
  }

  const firstSentenceMatch = jobDescription.match(/(.+?[.!?])(?:\s|$)/);
  const firstSentence = firstSentenceMatch?.[1] ?? jobDescription.slice(0, 240);
  return getNormalizedText(firstSentence);
}

function buildContact(
  type: HiringManagerContactType,
  value: string,
  label?: string
): HiringManagerContact | undefined {
  const normalizedValue = getNormalizedText(value);

  if (!normalizedValue) {
    return undefined;
  }

  return {
    type,
    value: normalizedValue,
    label: getNormalizedText(label)
  };
}

function dedupeContacts(contacts: HiringManagerContact[]): HiringManagerContact[] {
  const seenContacts = new Set<string>();

  return contacts.filter((contact) => {
    const key = `${contact.type}:${contact.value.toLowerCase()}`;

    if (seenContacts.has(key)) {
      return false;
    }

    seenContacts.add(key);
    return true;
  });
}

function isLikelyHiringContactAnchor(
  anchor: HTMLAnchorElement,
  hiringSection?: Element,
  restrictLinkedInProfiles?: boolean
): boolean {
  if (!restrictLinkedInProfiles) {
    return true;
  }

  if (hiringSection?.contains(anchor)) {
    return true;
  }

  const contextualContainer = anchor.closest('section, article, div');
  const contextText = getNormalizedText(contextualContainer?.textContent)?.toLowerCase();

  return Boolean(contextText && /(hiring|recruiter|talent|contact)/.test(contextText));
}

function extractContacts(
  documentRef: Document,
  text: string,
  options?: {
    hiringSection?: Element;
    restrictLinkedInProfiles?: boolean;
  }
): HiringManagerContact[] {
  const contacts: HiringManagerContact[] = [];

  for (const anchor of Array.from(documentRef.querySelectorAll<HTMLAnchorElement>('a[href]'))) {
    const href = anchor.getAttribute('href') || '';
    const label = getTextFromElement(anchor);

    if (href.startsWith('mailto:')) {
      const emailAddress = getMailtoEmailAddress(href);
      const contact = emailAddress ? buildContact('email', emailAddress, label) : undefined;

      if (contact) {
        contacts.push(contact);
      }
    }

    if (href.startsWith('tel:')) {
      const contact = buildContact('phone', href.replace(/^tel:/i, ''), label);

      if (contact) {
        contacts.push(contact);
      }
    }

    if (/linkedin\.com\/in\//i.test(href)) {
      if (!isLikelyHiringContactAnchor(anchor, options?.hiringSection, options?.restrictLinkedInProfiles)) {
        continue;
      }

      const contact = buildContact('linkedin', href, label);

      if (contact) {
        contacts.push(contact);
      }
    }
  }

  const emailMatches = text.match(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi) ?? [];
  const phoneMatches = text.match(/(?:\+?\d[\d\s().-]{7,}\d)/g) ?? [];

  for (const email of emailMatches.slice(0, 5)) {
    const contact = buildContact('email', email);

    if (contact) {
      contacts.push(contact);
    }
  }

  for (const phone of phoneMatches.slice(0, 5)) {
    const contact = buildContact('phone', phone);

    if (contact) {
      contacts.push(contact);
    }
  }

  return dedupeContacts(contacts);
}

function extractHiringManagerName(documentRef: Document, lines: string[]): string | undefined {
  for (const selector of HIRING_MANAGER_SELECTORS) {
    const name = getTextFromElement(documentRef.querySelector(selector));

    if (name && isLikelyPersonName(name)) {
      return name;
    }
  }

  const labeledName = extractLabeledValue(lines, HIRING_MANAGER_LABELS);
  return labeledName && isLikelyPersonName(labeledName) ? labeledName : undefined;
}

function extractHiringManagerNameFromContacts(
  documentRef: Document,
  text: string,
  contacts: HiringManagerContact[]
): string | undefined {
  const candidates: FieldCandidate[] = [];

  for (const contact of contacts) {
    if (contact.type !== 'email') {
      continue;
    }

    for (const contextText of getContextTextsForEmail(documentRef, contact.value)) {
      addFieldCandidate(
        candidates,
        extractBestPersonNameFromContext(contextText, contact.value),
        0.98,
        'contact-dom-context',
        isLikelyPersonName
      );
    }

    for (const textWindow of getTextWindowsAroundValue(text, contact.value)) {
      addFieldCandidate(
        candidates,
        extractBestPersonNameFromContext(textWindow, contact.value),
        0.94,
        'contact-text-window',
        isLikelyPersonName
      );
    }

    const escapedEmail = contact.value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const emailPattern = new RegExp(
      `(?:contact(?:[^\\n.]{0,120})?|questions?(?:[^\\n.]{0,120})?)\\b([A-Z][A-Za-zÀ-ÿ' -]{1,80})\\s*(?:,|\\(|-)\\s*${escapedEmail}`,
      'i'
    );
    const emailMatch = text.match(emailPattern);

    if (emailMatch?.[1]) {
      const personName = getNormalizedText(emailMatch[1]);

      addFieldCandidate(candidates, personName, 0.9, 'contact-regex', isLikelyPersonName);
    }

    addFieldCandidate(candidates, extractNameFromEmailAddress(contact.value), 0.78, 'email-local-part', isLikelyPersonName);
  }

  return pickBestCandidate(candidates);
}

function extractHiringManagerNameFromSection(section: Element | undefined): string | undefined {
  if (!section) {
    return undefined;
  }

  const directCandidate = getTextFromElement(section.querySelector('a[href*="/in/"], strong, h3'));

  if (directCandidate && isLikelyPersonName(directCandidate)) {
    return directCandidate;
  }

  const sectionLines = (section.textContent || '')
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

  const labeledName = extractLabeledValue(sectionLines, HIRING_MANAGER_LABELS);
  return labeledName && isLikelyPersonName(labeledName) ? labeledName : undefined;
}

function isLinkedInJobDetailPage(documentRef: Document): boolean {
  const pathname = documentRef.location.pathname.toLowerCase();

  if (pathname.includes('/jobs/view/')) {
    return true;
  }

  const hasJobTitle = Boolean(getFirstMatchingText(documentRef, LINKEDIN_JOB_TITLE_SELECTORS));
  const hasDescription = Boolean(
    getFirstMatchingText(documentRef, LINKEDIN_DESCRIPTION_SELECTORS) ||
      findSectionByHeading(documentRef, DESCRIPTION_LABELS)
  );
  const hasHiringTeam = Boolean(
    documentRef.querySelector(HIRING_MANAGER_SELECTORS.join(', ')) ||
      findSectionByHeading(documentRef, ['meet the hiring team', 'hiring team'])
  );

  return hasJobTitle && (hasDescription || hasHiringTeam);
}

function detectPageType(documentRef: Document, jsonLdJobPosting?: JsonLdJobPosting): DetectedPageType {
  const hostname = documentRef.location.hostname.toLowerCase();

  if (hostname.includes('linkedin.com') && isLinkedInJobDetailPage(documentRef)) {
    return 'linkedin-job';
  }

  if (jsonLdJobPosting) {
    return 'job-posting';
  }

  return 'generic-page';
}

function extractLinkedInDetails(documentRef: Document, textLines: string[]) {
  const descriptionText = getFirstMatchingText(documentRef, LINKEDIN_DESCRIPTION_SELECTORS);
  const fallbackDescriptionSection = findSectionByHeading(documentRef, DESCRIPTION_LABELS);
  const hiringSection = findSectionByHeading(documentRef, ['meet the hiring team', 'hiring team']);

  return {
    jobTitle: getFirstMatchingText(documentRef, LINKEDIN_JOB_TITLE_SELECTORS),
    companyName: getFirstMatchingText(documentRef, LINKEDIN_COMPANY_SELECTORS),
    location: getFirstMatchingText(documentRef, LINKEDIN_LOCATION_SELECTORS),
    jobDescription: descriptionText ?? getDescriptionFromSection(fallbackDescriptionSection),
    hiringManagerName:
      extractHiringManagerNameFromSection(hiringSection) ?? extractHiringManagerName(documentRef, textLines),
    hiringSection
  };
}

function extractLinkedInFeedDetails(documentRef: Document) {
  const candidates = Array.from(
    documentRef.querySelectorAll<HTMLAnchorElement>(LINKEDIN_FEED_JOB_CARD_SELECTORS.join(', '))
  );

  for (const candidate of candidates) {
    const title =
      getTextFromElement(candidate.querySelector('p span[aria-hidden="true"]')) ??
      getTextFromElement(candidate.querySelector('p'));

    if (!title) {
      continue;
    }

    const paragraphTexts = Array.from(candidate.querySelectorAll('p'))
      .map((paragraph) => getTextFromElement(paragraph))
      .filter((value): value is string => Boolean(value));

    const metadataTexts = paragraphTexts.filter(
      (value) => value !== title && !value.includes(title) && !title.includes(value)
    );

    let companyName: string | undefined;
    let location: string | undefined;

    for (const metadataText of metadataTexts) {
      const parts = metadataText
        .split('•')
        .map((part) => getNormalizedText(part))
        .filter((part): part is string => Boolean(part));

      if (!companyName && parts[0]) {
        companyName = parts[0];
      }

      if (!location && parts[1]) {
        location = parts[1];
      }
    }

    if (title || companyName || location) {
      return {
        jobTitle: title,
        companyName,
        location
      };
    }
  }

  return undefined;
}

function extractGenericDetails(documentRef: Document, textLines: string[]) {
  const descriptionSection = findSectionByHeading(documentRef, DESCRIPTION_LABELS);
  const jobTitleCandidates: FieldCandidate[] = [];
  const companyCandidates: FieldCandidate[] = [];
  const locationCandidates: FieldCandidate[] = [];
  const primaryTitle = getFirstMatchingText(documentRef, GENERIC_TITLE_SELECTORS);

  addFieldCandidate(jobTitleCandidates, primaryTitle, 0.96, 'generic-title');
  addFieldCandidate(companyCandidates, extractLabeledValueFromElements(documentRef, COMPANY_LABELS), 0.95, 'company-label-element', isLikelyCompanyValue);
  addFieldCandidate(companyCandidates, extractLabeledValue(textLines, COMPANY_LABELS), 0.9, 'company-label-line', isLikelyCompanyValue);
  addFieldCandidate(locationCandidates, extractLabeledValueFromElements(documentRef, LOCATION_LABELS), 0.98, 'location-label-element', isLikelyLocationValue);
  addFieldCandidate(locationCandidates, extractLabeledValue(textLines, LOCATION_LABELS), 0.92, 'location-label-line', isLikelyLocationValue);

  for (const text of collectSelectorTexts(documentRef, GENERIC_COMPANY_SELECTORS)) {
    addFieldCandidate(companyCandidates, text, 0.76, 'company-selector', isLikelyCompanyValue);
  }

  for (const text of collectSelectorTexts(documentRef, GENERIC_LOCATION_SELECTORS)) {
    addFieldCandidate(locationCandidates, text, 0.8, 'location-selector', isLikelyLocationValue);
  }

  for (const candidate of collectHeaderMetadataCandidates(documentRef, primaryTitle)) {
    if (candidate.source.includes('company')) {
      addFieldCandidate(companyCandidates, candidate.value, candidate.confidence, candidate.source, isLikelyCompanyValue);
      continue;
    }

    addFieldCandidate(locationCandidates, candidate.value, candidate.confidence, candidate.source, isLikelyLocationValue);
  }

  return {
    jobTitle: pickBestCandidate(jobTitleCandidates),
    companyName: pickBestCandidate(companyCandidates),
    location: pickBestCandidate(locationCandidates),
    jobDescription:
      getDescriptionFromSection(descriptionSection) ?? extractLabeledValue(textLines, DESCRIPTION_LABELS),
    hiringManagerName: extractHiringManagerName(documentRef, textLines)
  };
}

export function extractJobDetails(documentRef: Document, text: string): JobDetails {
  const textLines = text
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);
  const jsonLdJobPosting = extractJsonLdJobPosting(documentRef);
  const pageType = detectPageType(documentRef, jsonLdJobPosting);
  const metaTitle = getMetaContent(documentRef, 'og:title') ?? getMetaContent(documentRef, 'twitter:title');
  const metaDescription =
    getMetaContent(documentRef, 'description') ??
    getMetaContent(documentRef, 'og:description') ??
    getMetaContent(documentRef, 'twitter:description');
  const linkedInDetails = pageType === 'linkedin-job' ? extractLinkedInDetails(documentRef, textLines) : undefined;
  const linkedInFeedDetails =
    documentRef.location.hostname.toLowerCase().includes('linkedin.com') &&
    documentRef.location.pathname.toLowerCase().includes('/jobs/')
      ? extractLinkedInFeedDetails(documentRef)
      : undefined;
  const genericDetails = extractGenericDetails(documentRef, textLines);
  const hiringManagerContacts = extractContacts(documentRef, text, {
    hiringSection: linkedInDetails?.hiringSection,
    restrictLinkedInProfiles: documentRef.location.hostname.toLowerCase().includes('linkedin.com')
  });
  const jobDescription = normalizeDescription(
    linkedInDetails?.jobDescription ?? jsonLdJobPosting?.description ?? genericDetails.jobDescription
  );

  return {
    sourceHostname: documentRef.location.hostname,
    detectedPageType: pageType,
    jobTitle:
      linkedInDetails?.jobTitle ??
      linkedInFeedDetails?.jobTitle ??
      jsonLdJobPosting?.title ??
      genericDetails.jobTitle ??
      metaTitle,
    companyName:
      linkedInDetails?.companyName ??
      linkedInFeedDetails?.companyName ??
      jsonLdJobPosting?.companyName ??
      genericDetails.companyName ??
      getMetaContent(documentRef, 'og:site_name'),
    location:
      linkedInDetails?.location ??
      linkedInFeedDetails?.location ??
      jsonLdJobPosting?.location ??
      genericDetails.location,
    jobDescription,
    positionSummary: createPositionSummary(jobDescription, metaDescription),
    hiringManagerName:
      linkedInDetails?.hiringManagerName ??
      extractHiringManagerNameFromContacts(documentRef, text, hiringManagerContacts) ??
      genericDetails.hiringManagerName,
    hiringManagerContacts
  };
}
