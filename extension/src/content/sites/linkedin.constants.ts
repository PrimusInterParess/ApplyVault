export const LINKEDIN_JOB_TITLE_SELECTORS = [
  '.job-details-jobs-unified-top-card__job-title',
  '.jobs-unified-top-card__job-title',
  '.top-card-layout__title',
  '.topcard__title',
  'main h1',
  'h1'
];

export const LINKEDIN_COMPANY_SELECTORS = [
  '.job-details-jobs-unified-top-card__company-name',
  '.jobs-unified-top-card__company-name',
  '.topcard__org-name-link',
  '.topcard__flavor',
  '[data-test-job-details-company-name]',
  'a[href*="/company/"]'
];

export const LINKEDIN_LOCATION_SELECTORS = [
  '.job-details-jobs-unified-top-card__bullet',
  '.jobs-unified-top-card__bullet',
  '.topcard__flavor--bullet',
  '[data-test-job-details-location]',
  '[data-test-job-location]',
  '.job-details-jobs-unified-top-card__primary-description-container',
  '.jobs-unified-top-card__primary-description',
  '.job-details-jobs-unified-top-card__primary-description',
  '[class*="top-card"] [class*="primary-description"]',
  '.jobs-unified-top-card__subtitle-primary-grouping',
  'span.tvm__text--low-emphasis'
];

export const LINKEDIN_ACTIVE_JOB_CARD_SELECTORS = [
  '.jobs-search-results__list-item--active a[href*="/jobs/view/"]',
  '.jobs-search-results__list-item--active a[href*="/jobs/collections/"][href*="currentJobId="]',
  '[aria-current="page"][href*="/jobs/view/"]',
  '[aria-current="page"][href*="/jobs/collections/"][href*="currentJobId="]'
];

export const LINKEDIN_DESCRIPTION_SELECTORS = [
  '.jobs-description__content',
  '.jobs-box__html-content',
  '.show-more-less-html__markup',
  '[data-job-detail-container]'
];

export const LINKEDIN_FEED_JOB_CARD_SELECTORS = [
  'a[href*="/jobs/view/"]',
  'a[href*="/jobs/collections/"][href*="currentJobId="]'
];

export const HIRING_MANAGER_SELECTORS = [
  '.hirer-card__hirer-information a[href*="/in/"]',
  '.hirer-card__hirer-information strong',
  '.hirer-card__hirer-information h3',
  '[data-view-name="job-details-hiring-team"] a[href*="/in/"]',
  '[data-view-name="job-details-hiring-team"] strong'
];
