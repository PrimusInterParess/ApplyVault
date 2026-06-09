import { describe, expect, it } from 'vitest';
import { loadFixture } from '../../test/loadFixture';
import { extractJobDetails } from './extractJobDetails';

function extractWithText(fixture: string, url: string) {
  const { document } = loadFixture(fixture, url);
  const text = document.body?.innerText ?? '';
  return extractJobDetails(document, text);
}

describe('extractJobDetails', () => {
  it('extracts LinkedIn job detail page fields', () => {
    const details = extractWithText(
      'linkedin-job.html',
      'https://www.linkedin.com/jobs/view/1234567890/'
    );

    expect(details.detectedPageType).toBe('linkedin-job');
    expect(details.jobTitle).toBe('Senior Software Engineer');
    expect(details.companyName).toBe('ApplyVault ApS');
    expect(details.location).toBe('Copenhagen, Denmark');
    expect(details.jobDescription).toContain('Senior Software Engineer');
    expect(details.hiringManagerName).toBe('Jane Recruiter');
  });

  it('extracts generic job page fields', () => {
    const details = extractWithText('generic-job.html', 'https://example.com/careers/backend-developer');

    expect(details.detectedPageType).toBe('job-posting');
    expect(details.jobTitle).toBe('Backend Developer');
    expect(details.companyName).toBe('Nordic Jobs AS');
    expect(details.location).toBe('Oslo, Norway');
    expect(details.jobDescription).toContain('Backend Developer');
  });

  it('extracts JSON-LD job posting fields', () => {
    const details = extractWithText('jsonld-job.html', 'https://careers.schemacorp.com/jobs/platform-engineer');

    expect(details.detectedPageType).toBe('job-posting');
    expect(details.jobTitle).toBe('Platform Engineer');
    expect(details.companyName).toBe('Schema Corp');
    expect(details.location).toContain('Aarhus');
    expect(details.jobDescription).toContain('Platform Engineer');
  });

  it('extracts LinkedIn feed card fields', () => {
    const details = extractWithText('linkedin-feed.html', 'https://www.linkedin.com/jobs/collections/recommended/');

    expect(details.jobTitle).toBe('Product Manager');
    expect(details.companyName).toBe('FeedCo');
    expect(details.location).toBe('Berlin, Germany');
  });

  it('extracts LinkedIn feed card location from separate metadata lines', () => {
    const details = extractWithText(
      'linkedin-feed-separate-lines.html',
      'https://www.linkedin.com/jobs/collections/recommended/'
    );

    expect(details.jobTitle).toBe('IT Infrastructure & Support Engineer');
    expect(details.companyName).toBe('InCommodities');
    expect(details.location).toBe('Aarhus Municipality, Central Denmark Region, Denmark (Hybrid)');
  });

  it('extracts LinkedIn job location from combined company and location metadata', () => {
    const details = extractWithText(
      'linkedin-job-combined-metadata.html',
      'https://www.linkedin.com/jobs/view/1234567890/'
    );

    expect(details.detectedPageType).toBe('linkedin-job');
    expect(details.jobTitle).toBe('IT Infrastructure & Support Engineer');
    expect(details.location).toBe('Aarhus Municipality, Central Denmark Region, Denmark (Hybrid)');
  });

  it('does not use the company name as LinkedIn job location', () => {
    const details = extractWithText(
      'linkedin-job-separate-metadata.html',
      'https://www.linkedin.com/jobs/view/1234567890/'
    );

    expect(details.companyName).toBe('InCommodities');
    expect(details.location).toBe('Aarhus Municipality, Central Denmark Region, Denmark (Hybrid)');
    expect(details.location).not.toBe(details.companyName);
  });

  it('detects Teamtailor inactive listing without description fallback', () => {
    const details = extractWithText(
      'teamtailor-inactive.html',
      'https://acme.teamtailor.com/jobs/marketing-lead'
    );

    expect(details.jobTitle).toBe('Marketing Lead');
    expect(details.jobDescription).toBeUndefined();
  });
});
