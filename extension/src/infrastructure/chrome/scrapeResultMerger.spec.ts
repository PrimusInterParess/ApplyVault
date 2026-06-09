import { describe, expect, it } from 'vitest';
import type { ScrapeResult } from '../../shared/models/scrapeResult';
import { mergeScrapeResults } from './scrapeResultMerger';

function createResult(overrides: Partial<ScrapeResult> = {}): ScrapeResult {
  return {
    title: 'Job',
    url: 'https://example.com/jobs/1',
    text: 'Primary frame text content that is long enough for merge tests.',
    textLength: 60,
    extractedAt: new Date().toISOString(),
    jobDetails: {
      sourceHostname: 'example.com',
      detectedPageType: 'job-posting',
      jobTitle: 'Primary Title',
      companyName: 'Primary Co',
      hiringManagerContacts: []
    },
    ...overrides
  };
}

describe('mergeScrapeResults', () => {
  it('prefers primary structured fields and merges text', () => {
    const merged = mergeScrapeResults(
      createResult(),
      createResult({
        text: 'Fallback description with additional details from the main frame.',
        jobDetails: {
          sourceHostname: 'example.com',
          detectedPageType: 'generic-page',
          jobTitle: 'Fallback Title',
          companyName: 'Fallback Co',
          location: 'Remote',
          hiringManagerContacts: []
        }
      })
    );

    expect(merged.jobDetails.jobTitle).toBe('Primary Title');
    expect(merged.jobDetails.location).toBe('Remote');
    expect(merged.text).toContain('Primary frame text');
    expect(merged.text).toContain('Fallback description');
  });
});
