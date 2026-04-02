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
  '[data-test-job-location]'
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

export const DESCRIPTION_LABELS = ['job description', 'about the job', 'description', 'about this role'];
export const COMPANY_LABELS = ['company', 'company name', 'organization', 'employer'];
export const LOCATION_LABELS = ['location', 'job location', 'based in', 'arbejdssted', 'lokation', 'arbejdsplads'];
export const HIRING_MANAGER_LABELS = ['hiring manager', 'recruiter', 'contact'];

export const NON_PERSON_HIRING_MANAGER_TERMS = [
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

export const NON_METADATA_TERMS = [
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

export const NON_PERSON_EMAIL_LOCALPART_TERMS = [
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

export const DESCRIPTION_SKIPPED_TAGS = new Set([
  'button',
  'canvas',
  'footer',
  'form',
  'iframe',
  'img',
  'input',
  'nav',
  'noscript',
  'script',
  'select',
  'style',
  'svg',
  'textarea'
]);

export const DESCRIPTION_BLOCK_TAGS = new Set([
  'article',
  'aside',
  'blockquote',
  'div',
  'dl',
  'fieldset',
  'figcaption',
  'figure',
  'footer',
  'form',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'header',
  'hr',
  'li',
  'main',
  'ol',
  'p',
  'pre',
  'section',
  'table',
  'tbody',
  'td',
  'tfoot',
  'th',
  'thead',
  'tr',
  'ul'
]);

export const SAFE_DESCRIPTION_LINK_PROTOCOLS = new Set(['http:', 'https:', 'mailto:', 'tel:']);
