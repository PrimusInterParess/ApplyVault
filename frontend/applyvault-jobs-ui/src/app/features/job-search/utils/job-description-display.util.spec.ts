import { resolveJobDescriptionDisplayMode } from './job-description-display.util';

describe('resolveJobDescriptionDisplayMode', () => {
  it('returns empty for missing job', () => {
    expect(resolveJobDescriptionDisplayMode(null)).toBe('empty');
  });

  it('returns preview for previewOnly listings', () => {
    expect(
      resolveJobDescriptionDisplayMode({
        description: null,
        descriptionQuality: 'previewOnly',
        descriptionExcerpt: 'Developer role'
      })
    ).toBe('preview');
  });

  it('returns full when description is present', () => {
    expect(
      resolveJobDescriptionDisplayMode({
        description: '<p>Build APIs</p>',
        descriptionQuality: 'full',
        descriptionExcerpt: null
      })
    ).toBe('full');
  });

  it('returns empty when no description is available', () => {
    expect(
      resolveJobDescriptionDisplayMode({
        description: null,
        descriptionQuality: 'full',
        descriptionExcerpt: null
      })
    ).toBe('empty');
  });
});
