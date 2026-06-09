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
