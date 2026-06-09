import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { JSDOM } from 'jsdom';

const fixturesDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../content/__fixtures__');

export function loadFixture(filename: string, pageUrl = 'https://example.com/jobs/test'): { document: Document } {
  const html = readFileSync(path.join(fixturesDir, filename), 'utf-8');
  const dom = new JSDOM(html, { url: pageUrl });

  return {
    document: dom.window.document
  };
}
