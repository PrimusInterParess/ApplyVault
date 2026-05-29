import { renderJobDescription } from './job-description-render.util';

describe('renderJobDescription', () => {
  it('returns empty string for blank input', () => {
    expect(renderJobDescription(null)).toBe('');
    expect(renderJobDescription(undefined)).toBe('');
    expect(renderJobDescription('   ')).toBe('');
  });

  it('preserves safe HTML structure', () => {
    const html = '<p>Build <strong>APIs</strong></p>';

    expect(renderJobDescription(html)).toContain('<p>');
    expect(renderJobDescription(html)).toContain('<strong>APIs</strong>');
  });

  it('converts div wrappers into paragraphs', () => {
    const html = '<div>First block</div><div>Second block</div>';
    const rendered = renderJobDescription(html);

    expect(rendered).toContain('<p>First block</p>');
    expect(rendered).toContain('<p>Second block</p>');
  });

  it('renders plain text with paragraph breaks via markdown', () => {
    const rendered = renderJobDescription('First paragraph.\n\nSecond paragraph.');

    expect(rendered).toContain('<p>');
    expect(rendered).toContain('First paragraph.');
    expect(rendered).toContain('Second paragraph.');
  });

  it('collapses excessive blank lines in plain text', () => {
    const rendered = renderJobDescription('Intro\n\n\n\n\nDetails');

    expect(rendered).toContain('Intro');
    expect(rendered).toContain('Details');
    expect(rendered).not.toMatch(/(<br\s*\/?>\s*){3,}/i);
  });

  it('renders plain-text bullet lines as lists', () => {
    const rendered = renderJobDescription('Role summary\n\n- Build APIs\n- Ship features');

    expect(rendered).toContain('<ul>');
    expect(rendered).toContain('Build APIs');
    expect(rendered).toContain('Ship features');
  });

  it('formats scraped plain text that only contains line breaks', () => {
    const rendered = renderJobDescription('Developer role\n\n\nPrinciples\n\n\nServices');

    expect(rendered).toContain('Developer role');
    expect(rendered).toContain('Principles');
    expect(rendered).toContain('Services');
  });
});
