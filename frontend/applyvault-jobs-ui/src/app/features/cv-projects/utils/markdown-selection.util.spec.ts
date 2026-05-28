import {
  prefixMarkdownLine,
  wrapMarkdownSelection
} from './markdown-selection.util';

describe('markdown-selection.util', () => {
  it('wraps bold around selection', () => {
    const result = wrapMarkdownSelection('Led team', 0, 3, 'bold');

    expect(result.value).toBe('**Led** team');
    expect(result.selectionStart).toBe(2);
    expect(result.selectionEnd).toBe(5);
  });

  it('wraps italic with placeholder when empty', () => {
    const result = wrapMarkdownSelection('hello', 5, 5, 'italic');

    expect(result.value).toBe('hello*italic text*');
  });

  it('inserts link with url selection', () => {
    const result = wrapMarkdownSelection('see docs', 4, 4, 'link');

    expect(result.value).toBe('see [link text](url)docs');
  });

  it('prefixes bullet on current line', () => {
    const result = prefixMarkdownLine('First line\nSecond', 11, 17, '- ');

    expect(result.value).toBe('First line\n- Second');
  });
});
