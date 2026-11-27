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

  it('captures the posting text without scripts or hidden content from a detached rendered-text fallback', () => {
    const { document } = loadFixture('generic-job.html');
    const view = document.defaultView;
    const main = document.querySelector('main');

    if (!view || !main) {
      throw new Error('Fixture needs a window and main element.');
    }

    // Detached DOM elements can return textContent from innerText in Chromium.
    Object.defineProperty(view.HTMLElement.prototype, 'innerText', {
      configurable: true,
      get: function (this: HTMLElement): string {
        return this.textContent ?? '';
      }
    });
    const script = document.createElement('script');
    script.textContent = `window.internalConfiguration = '${'SCRIPT_PAYLOAD '.repeat(1000)}';`;
    main.prepend(script);
    const hidden = document.createElement('div');
    hidden.style.display = 'none';
    hidden.textContent = 'HIDDEN_TEMPLATE '.repeat(1000);
    main.append(hidden);

    const result = extractVisibleText(document);

    expect(result.text).toContain('Nordic Jobs is hiring a Backend Developer');
    expect(result.text).not.toContain('SCRIPT_PAYLOAD');
    expect(result.text).not.toContain('HIDDEN_TEMPLATE');
    expect(result.jobDetails.jobDescription).toContain('PostgreSQL');
    expect(document.querySelector('script')).toBe(script);
  });

  it('does not save a return-to-list navigation link as the employer', () => {
    const { document } = loadFixture('generic-job.html');
    document.querySelector('dl')?.remove();
    const link = document.createElement('a');
    link.className = 'company';
    link.href = '/jobs';
    link.textContent = 'Return to List';
    document.querySelector('h1')?.after(link);

    const result = extractVisibleText(document);

    expect(result.jobDetails.companyName).toBeUndefined();
    expect(result.jobDetails.jobDescription).toContain('PostgreSQL');
  });
});
