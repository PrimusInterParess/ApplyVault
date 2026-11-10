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

  it('promotes a short bold paragraph into a section heading', () => {
    const rendered = renderJobDescription('<p><strong>Requirements</strong></p><p>Build APIs.</p>');

    expect(rendered).toContain('<h3>Requirements</h3>');
    expect(rendered).toContain('<p>Build APIs.</p>');
  });

  it('keeps a bold sentence as a paragraph', () => {
    const rendered = renderJobDescription('<p><strong>We build reliable APIs for banks.</strong></p>');

    expect(rendered).toContain('<p><strong>We build reliable APIs for banks.</strong></p>');
    expect(rendered).not.toContain('<h3>');
  });

  it('promotes a short label in front of a list', () => {
    const rendered = renderJobDescription('Role summary\n\n- Build APIs\n- Ship features');

    expect(rendered).toContain('<h3>Role summary</h3>');
    expect(rendered).toContain('<ul>');
  });

  it('repairs broken markdown headings from a Work in Denmark listing', () => {
    const html = [
      '<p>_Copenhagen, Denmark</p>',
      '<p>_**About The Job</p>',
      '<p>**Do you want to work with data engineering at Saxo?</p>',
      '<p>**Responsibilities</p>',
      '<p>**</p>',
      '<ul><li>Build data products.</li></ul>',
      '<p>**Your profile</p>',
      '<p>**</p>',
      '<ul><li>5+ years of experience.</li></ul>',
      '<p>**How To Apply</p>',
      '<p>**Click here to create an account and upload your resume.</p>'
    ].join('\n');

    const rendered = renderJobDescription(html);

    expect(rendered).toContain('<p>Copenhagen, Denmark</p>');
    expect(rendered).toContain('<h3>About The Job</h3>');
    expect(rendered).toContain('<h3>Responsibilities</h3>');
    expect(rendered).toContain('<h3>Your profile</h3>');
    expect(rendered).toContain('<h3>How To Apply</h3>');
    expect(rendered).toContain('<p>Do you want to work with data engineering at Saxo?</p>');
    expect(rendered).toContain('<p>Click here to create an account and upload your resume.</p>');
    expect(rendered).not.toContain('**');
    expect(rendered).not.toContain('_Copenhagen');
  });

  it('formats scraped plain text that only contains line breaks', () => {
    const rendered = renderJobDescription('Developer role\n\n\nPrinciples\n\n\nServices');

    expect(rendered).toContain('Developer role');
    expect(rendered).toContain('Principles');
    expect(rendered).toContain('Services');
  });
});
