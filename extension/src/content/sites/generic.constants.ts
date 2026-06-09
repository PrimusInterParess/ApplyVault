export const GENERIC_TITLE_SELECTORS = ['main h1', 'article h1', 'header h1', 'h1'];

export const GENERIC_COMPANY_SELECTORS = [
  '[itemprop="hiringOrganization"]',
  '[data-test*="company"]',
  '[data-qa*="company"]',
  '[class*="company"]',
  '[class*="employer"]',
  '[class*="organization"]'
];

export const GENERIC_LOCATION_SELECTORS = [
  '[itemprop="jobLocation"]',
  '[data-test*="location"]',
  '[data-qa*="location"]',
  '[class*="location"]',
  '[class*="job-location"]',
  '[class*="office"]',
  '[class*="city"]',
  'address'
];

export const GENERIC_DESCRIPTION_SELECTORS = [
  '[itemprop="description"]',
  '[data-test*="description"]',
  '[data-qa*="description"]',
  '[class*="job-description"]',
  '[class*="posting-description"]',
  '[class*="description__content"]',
  '[class*="description__text"]',
  '[class*="job-details"] article',
  '[role="main"] article',
  'main article',
  'article'
];
