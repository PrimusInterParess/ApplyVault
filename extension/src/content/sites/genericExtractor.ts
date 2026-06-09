import { field } from '../../domain/extraction/fieldResolver';
import type { ExtractionContext } from '../../domain/extraction/types';
import type { FieldExtraction } from '../../domain/extraction/types';
import { COMPANY_LABELS, DESCRIPTION_LABELS, LOCATION_LABELS } from '../dom/constants';
import { extractHiringManagerName } from '../jobDetailsExtraction/contacts';
import {
  getBestGenericDescription,
  getDescriptionFromSection
} from '../jobDetailsExtraction/description';
import {
  collectHeaderMetadataCandidates,
  isLikelyCompanyValue,
  isLikelyLocationValue
} from '../jobDetailsExtraction/metadata';
import {
  addFieldCandidate,
  collectSelectorTexts,
  extractLabeledValue,
  extractLabeledValueFromElements,
  findSectionByHeading,
  getFirstMatchingText,
  pickBestCandidate
} from '../jobDetailsExtraction/shared';
import type { FieldCandidate } from '../jobDetailsExtraction/types';
import {
  GENERIC_COMPANY_SELECTORS,
  GENERIC_LOCATION_SELECTORS,
  GENERIC_TITLE_SELECTORS
} from './generic.constants';
import type { SiteExtractor } from './types';

export const genericExtractor: SiteExtractor = {
  id: 'generic',

  canHandle(): boolean {
    return true;
  },

  extract(ctx: ExtractionContext): FieldExtraction[] {
    const { document, textLines } = ctx;
    const descriptionSection = findSectionByHeading(document, DESCRIPTION_LABELS);
    const jobTitleCandidates: FieldCandidate[] = [];
    const companyCandidates: FieldCandidate[] = [];
    const locationCandidates: FieldCandidate[] = [];
    const primaryTitle = getFirstMatchingText(document, GENERIC_TITLE_SELECTORS);

    addFieldCandidate(jobTitleCandidates, primaryTitle, 0.96, 'generic-title');
    addFieldCandidate(
      companyCandidates,
      extractLabeledValueFromElements(document, COMPANY_LABELS),
      0.95,
      'company-label-element',
      isLikelyCompanyValue
    );
    addFieldCandidate(
      companyCandidates,
      extractLabeledValue(textLines, COMPANY_LABELS),
      0.9,
      'company-label-line',
      isLikelyCompanyValue
    );
    addFieldCandidate(
      locationCandidates,
      extractLabeledValueFromElements(document, LOCATION_LABELS),
      0.98,
      'location-label-element',
      isLikelyLocationValue
    );
    addFieldCandidate(
      locationCandidates,
      extractLabeledValue(textLines, LOCATION_LABELS),
      0.92,
      'location-label-line',
      isLikelyLocationValue
    );

    for (const text of collectSelectorTexts(document, GENERIC_COMPANY_SELECTORS)) {
      addFieldCandidate(companyCandidates, text, 0.76, 'company-selector', isLikelyCompanyValue);
    }

    for (const text of collectSelectorTexts(document, GENERIC_LOCATION_SELECTORS)) {
      addFieldCandidate(locationCandidates, text, 0.8, 'location-selector', isLikelyLocationValue);
    }

    for (const candidate of collectHeaderMetadataCandidates(document, primaryTitle)) {
      if (candidate.source.includes('company')) {
        addFieldCandidate(companyCandidates, candidate.value, candidate.confidence, candidate.source, isLikelyCompanyValue);
        continue;
      }

      addFieldCandidate(locationCandidates, candidate.value, candidate.confidence, candidate.source, isLikelyLocationValue);
    }

    const jobDescription =
      getBestGenericDescription(document) ??
      getDescriptionFromSection(descriptionSection) ??
      extractLabeledValue(textLines, DESCRIPTION_LABELS);

    return [
      ...field(pickBestCandidate(jobTitleCandidates), 'jobTitle', 0.96, 'generic'),
      ...field(pickBestCandidate(companyCandidates), 'companyName', 0.94, 'generic'),
      ...field(pickBestCandidate(locationCandidates), 'location', 0.94, 'generic'),
      ...field(jobDescription, 'jobDescription', 0.92, 'generic'),
      ...field(extractHiringManagerName(document, textLines), 'hiringManagerName', 0.85, 'generic')
    ];
  }
};
