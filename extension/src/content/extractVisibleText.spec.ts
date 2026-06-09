import { describe, expect, it } from 'vitest';
import { loadFixture } from '../test/loadFixture';
import { extractVisibleText } from './extractVisibleText';

describe('extractVisibleText', () => {
  it('returns non-empty text for a generic job page', () => {
    const { document } = loadFixture('generic-job.html', 'https://example.com/careers/backend-developer');
    const result = extractVisibleText(document);

    expect(result.text.length).toBeGreaterThan(200);
    expect(result.text).toContain('Backend Developer');
    expect(result.jobDetails.jobTitle).toBe('Backend Developer');
    expect(result.url).toBe('https://example.com/careers/backend-developer');
  });

  it('returns structured job details for LinkedIn fixture', () => {
    const { document } = loadFixture(
      'linkedin-job.html',
      'https://www.linkedin.com/jobs/view/1234567890/'
    );
    const result = extractVisibleText(document);

    expect(result.textLength).toBeGreaterThan(0);
    expect(result.jobDetails.detectedPageType).toBe('linkedin-job');
    expect(result.jobDetails.companyName).toBe('ApplyVault ApS');
  });
});
