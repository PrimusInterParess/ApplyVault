import type { JobDetails } from '../../shared/models/scrapeResult';
import { extractHiringManagerNameFromContacts, extractContacts } from './contacts';
import { normalizeDescription } from './description';
import { extractJsonLdJobPosting } from './jsonLd';
import {
  detectPageType,
  extractGenericDetails,
  extractLinkedInDetails,
  extractLinkedInFeedDetails
} from './pageExtractors';
import { createPositionSummary, getMetaContent } from './shared';

function hasInactiveListingSignal(documentRef: Document, text: string): boolean {
  const hostname = documentRef.location.hostname.toLowerCase();

  if (!hostname.includes('teamtailor.com')) {
    return false;
  }

  return /\b(?:this job is no longer active|position has been filled|listing has expired|jobopslag er ikke længere aktivt|jobbet besat|opslaget er udløbet|stillingen er ikke længere aktiv)\b/i.test(
    text
  );
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
  const listingIsInactive = hasInactiveListingSignal(documentRef, text);
  const hiringManagerContacts = extractContacts(documentRef, text, {
    hiringSection: linkedInDetails?.hiringSection,
    restrictLinkedInProfiles: documentRef.location.hostname.toLowerCase().includes('linkedin.com')
  });
  const jobDescription = normalizeDescription(
    linkedInDetails?.jobDescription ??
      genericDetails.jobDescription ??
      (listingIsInactive ? undefined : jsonLdJobPosting?.description),
    documentRef
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
    positionSummary: createPositionSummary(jobDescription, listingIsInactive ? undefined : metaDescription),
    hiringManagerName:
      linkedInDetails?.hiringManagerName ??
      extractHiringManagerNameFromContacts(documentRef, text, hiringManagerContacts) ??
      genericDetails.hiringManagerName,
    hiringManagerContacts
  };
}
