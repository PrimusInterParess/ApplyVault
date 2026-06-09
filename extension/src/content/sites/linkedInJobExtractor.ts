import { field } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import { DESCRIPTION_LABELS } from '../dom/constants';
import {
  extractHiringManagerName,
  extractHiringManagerNameFromSection
} from '../jobDetailsExtraction/contacts';
import {
  getDescriptionFromSection,
  getFirstMatchingDescription
} from '../jobDetailsExtraction/description';
import { findSectionByHeading, getFirstMatchingText } from '../jobDetailsExtraction/shared';
import {
  LINKEDIN_COMPANY_SELECTORS,
  LINKEDIN_DESCRIPTION_SELECTORS,
  LINKEDIN_JOB_TITLE_SELECTORS
} from './linkedin.constants';
import { extractLinkedInLocation } from './linkedInLocation';
import type { SiteExtractor } from './types';

export const linkedInJobExtractor: SiteExtractor = {
  id: 'linkedin-job',

  canHandle(ctx: ExtractionContext): boolean {
    return ctx.pageType === 'linkedin-job';
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const { document, textLines } = ctx;
    const descriptionText = getFirstMatchingDescription(document, LINKEDIN_DESCRIPTION_SELECTORS);
    const fallbackDescriptionSection = findSectionByHeading(document, DESCRIPTION_LABELS);
    const hiringSection = findSectionByHeading(document, ['meet the hiring team', 'hiring team']);
    const jobDescription = descriptionText ?? getDescriptionFromSection(fallbackDescriptionSection);
    const hiringManagerName =
      extractHiringManagerNameFromSection(hiringSection) ?? extractHiringManagerName(document, textLines);

    return [
      ...field(getFirstMatchingText(document, LINKEDIN_JOB_TITLE_SELECTORS), 'jobTitle', 0.97, 'linkedin-job'),
      ...field(getFirstMatchingText(document, LINKEDIN_COMPANY_SELECTORS), 'companyName', 0.96, 'linkedin-job'),
      ...field(extractLinkedInLocation(document, textLines), 'location', 0.95, 'linkedin-job'),
      ...field(jobDescription, 'jobDescription', 0.96, 'linkedin-job'),
      ...field(hiringManagerName, 'hiringManagerName', 0.9, 'linkedin-job')
    ];
  }
};
