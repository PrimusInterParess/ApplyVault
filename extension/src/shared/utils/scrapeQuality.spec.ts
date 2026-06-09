import { describe, expect, it } from 'vitest';
import type { ScrapeResult } from '../models/scrapeResult';
import { evaluateScrapeResult, getScrapeResultScore } from './scrapeQuality';

function createResult(overrides: Partial<ScrapeResult> = {}): ScrapeResult {
  const description =
    'A sufficiently long job description that exceeds the minimum threshold for quality evaluation and should not trigger short-description warnings in the scraper quality module.';

  return {
    title: 'Job page',
    url: 'https://example.com/jobs/test',
    text: 'x'.repeat(250),
    textLength: 250,
    extractedAt: new Date().toISOString(),
    jobDetails: {
      sourceHostname: 'example.com',
      detectedPageType: 'job-posting',
      jobTitle: 'Software Engineer',
      companyName: 'Example Corp',
      location: 'Remote',
      jobDescription: description,
      positionSummary: 'Summary',
      hiringManagerContacts: []
    },
    ...overrides
  };
}

describe('scrapeQuality', () => {
  it('marks complete captures as valid', () => {
    const metadata = evaluateScrapeResult(createResult());

    expect(metadata.status).toBe('valid');
    expect(metadata.issues).toHaveLength(0);
  });

  it('marks missing core fields as invalid', () => {
    const metadata = evaluateScrapeResult(
      createResult({
        text: 'short',
        textLength: 10,
        jobDetails: {
          sourceHostname: 'example.com',
          detectedPageType: 'generic-page',
          hiringManagerContacts: []
        }
      })
    );

    expect(metadata.status).toBe('invalid');
    expect(metadata.issues.some((issue) => issue.code === 'missing-title')).toBe(true);
    expect(metadata.issues.some((issue) => issue.code === 'missing-description')).toBe(true);
  });

  it('scores valid results higher than partial results', () => {
    const validScore = getScrapeResultScore(createResult());
    const partialScore = getScrapeResultScore(
      createResult({
        jobDetails: {
          ...createResult().jobDetails,
          companyName: undefined
        }
      })
    );

    expect(validScore).toBeGreaterThan(partialScore);
  });
});
