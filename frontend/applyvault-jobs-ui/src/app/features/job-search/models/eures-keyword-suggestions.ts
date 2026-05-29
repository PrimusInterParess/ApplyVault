export type EuresKeywordSuggestionGroup = {
  readonly label: string;
  readonly keywords: readonly string[];
};

export const EURES_KEYWORD_SUGGESTION_GROUPS: readonly EuresKeywordSuggestionGroup[] = [
  {
    label: 'Roles',
    keywords: [
      'software',
      'developer',
      'backend',
      'frontend',
      'fullstack',
      'devops',
      'architect',
      'consultant',
      'support',
      'QA'
    ]
  },
  {
    label: 'Languages',
    keywords: [
      'C#',
      'Java',
      'Python',
      'JavaScript',
      'TypeScript',
      'Go',
      'Kotlin',
      'PHP',
      'Ruby',
      'Rust'
    ]
  },
  {
    label: 'Frameworks',
    keywords: [
      'dotnet',
      '.NET',
      'React',
      'Angular',
      'Vue',
      'Node.js',
      'Spring',
      'Django',
      'Next.js',
      'Flutter',
      'Blazor'
    ]
  },
  {
    label: 'Cloud & DevOps',
    keywords: [
      'Azure',
      'AWS',
      'GCP',
      'Kubernetes',
      'Docker',
      'Terraform',
      'Linux',
      'Git',
      'CI/CD',
      'microservices'
    ]
  },
  {
    label: 'Data & databases',
    keywords: [
      'SQL',
      'PostgreSQL',
      'MongoDB',
      'Redis',
      'Kafka',
      'Spark',
      'Power BI',
      'data engineer',
      'analytics',
      'machine learning'
    ]
  },
  {
    label: 'Danish',
    keywords: [
      'udvikler',
      'softwareudvikler',
      'programmør',
      'it-supporter',
      'systemadministrator',
      'datamatiker',
      'konsulent',
      'tester'
    ]
  }
];
