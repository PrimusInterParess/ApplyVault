const PAGE_WIDTH = 595.28;
const PAGE_HEIGHT = 841.89;
const MARGIN = 72;
const FONT_SIZE = 12;
const LEADING = 16;
const CONTENT_WIDTH = PAGE_WIDTH - MARGIN * 2;
const START_Y = PAGE_HEIGHT - MARGIN - FONT_SIZE;
const LINES_PER_PAGE = Math.floor((START_Y - MARGIN) / LEADING) + 1;

/** Times-Roman widths in 1000-unit ems. Unknown glyphs use a safe default. */
const GLYPH_WIDTHS: Record<string, number> = {
  ' ': 250, '!': 333, '"': 408, '#': 500, '$': 500, '%': 833, '&': 778, "'": 180,
  '(': 333, ')': 333, '*': 500, '+': 564, ',': 250, '-': 333, '.': 250, '/': 278,
  '0': 500, '1': 500, '2': 500, '3': 500, '4': 500, '5': 500, '6': 500, '7': 500, '8': 500, '9': 500,
  ':': 278, ';': 278, '<': 564, '=': 564, '>': 564, '?': 444, '@': 921,
  A: 722, B: 667, C: 667, D: 722, E: 611, F: 556, G: 722, H: 722, I: 333, J: 389,
  K: 722, L: 611, M: 889, N: 722, O: 722, P: 556, Q: 722, R: 667, S: 556, T: 611,
  U: 722, V: 722, W: 944, X: 722, Y: 722, Z: 611,
  '[': 333, '\\': 278, ']': 333, '^': 469, _: 500, '`': 333,
  a: 444, b: 500, c: 444, d: 500, e: 444, f: 333, g: 500, h: 500, i: 278, j: 278,
  k: 500, l: 278, m: 778, n: 500, o: 500, p: 500, q: 500, r: 333, s: 389, t: 278,
  u: 500, v: 500, w: 722, x: 500, y: 500, z: 444,
  '{': 480, '|': 200, '}': 480, '~': 541,
  '\u2013': 500, '\u2014': 1000, '\u2018': 333, '\u2019': 333, '\u201C': 444, '\u201D': 444,
  '\u2026': 1000, '\u2022': 350, '\u20AC': 500
};

const DEFAULT_GLYPH_WIDTH = 500;

/** Unicode punctuation that WinAnsi (Windows-1252) stores in the 0x80–0x9F range. */
const WINANSI_SPECIALS: Record<string, number> = {
  '\u20AC': 0x80,
  '\u201A': 0x82,
  '\u0192': 0x83,
  '\u201E': 0x84,
  '\u2026': 0x85,
  '\u2020': 0x86,
  '\u2021': 0x87,
  '\u02C6': 0x88,
  '\u2030': 0x89,
  '\u0160': 0x8a,
  '\u2039': 0x8b,
  '\u0152': 0x8c,
  '\u017D': 0x8e,
  '\u2018': 0x91,
  '\u2019': 0x92,
  '\u201C': 0x93,
  '\u201D': 0x94,
  '\u2022': 0x95,
  '\u2013': 0x96,
  '\u2014': 0x97,
  '\u02DC': 0x98,
  '\u2122': 0x99,
  '\u0161': 0x9a,
  '\u203A': 0x9b,
  '\u0153': 0x9c,
  '\u017E': 0x9e,
  '\u0178': 0x9f
};

const INVALID_FILE_NAME_CHARS = /[<>:"/\\|?*\u0000-\u001f]/g;

export function buildCoverLetterPdfFileName(company: string | null | undefined): string {
  const segment = (company ?? '')
    .normalize('NFKC')
    .trim()
    .replace(/\s+/g, '-')
    .replace(INVALID_FILE_NAME_CHARS, '')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 80);

  return segment ? `${segment}-Cover-Letter.pdf` : 'Cover-Letter.pdf';
}

export function buildCoverLetterPdfBytes(letter: string): Uint8Array {
  const lines = paginate(layoutLines(letter));
  return encodePdf(lines.map((page) => pageContent(page)), 'Cover letter');
}

export function downloadCoverLetterPdf(letter: string, fileName: string): void {
  const blob = new Blob([buildCoverLetterPdfBytes(letter)], { type: 'application/pdf' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

function layoutLines(letter: string): string[] {
  const normalized = letter.replace(/\r\n/g, '\n').replace(/\r/g, '\n').replace(/\u00A0/g, ' ').replace(/\u00AD/g, '').trim();
  if (!normalized) {
    return [''];
  }

  const lines: string[] = [];
  for (const paragraph of normalized.split('\n')) {
    lines.push(...wrapLine(paragraph.trim(), CONTENT_WIDTH));
  }

  return lines;
}

function wrapLine(text: string, maxWidth: number): string[] {
  if (!text) {
    return [''];
  }

  const lines: string[] = [];
  let current = '';

  const pushCurrent = (): void => {
    if (current) {
      lines.push(current);
      current = '';
    }
  };

  for (const word of text.split(/\s+/)) {
    if (measure(word) <= maxWidth) {
      const candidate = current ? `${current} ${word}` : word;
      if (measure(candidate) <= maxWidth) {
        current = candidate;
      } else {
        pushCurrent();
        current = word;
      }
      continue;
    }

    pushCurrent();
    let chunk = '';
    for (const character of word) {
      const next = chunk + character;
      if (chunk && measure(next) > maxWidth) {
        lines.push(chunk);
        chunk = character;
      } else {
        chunk = next;
      }
    }
    current = chunk;
  }

  pushCurrent();
  return lines.length > 0 ? lines : [''];
}

function paginate(lines: string[]): string[][] {
  const pages: string[][] = [];
  for (let index = 0; index < lines.length; index += LINES_PER_PAGE) {
    pages.push(lines.slice(index, index + LINES_PER_PAGE));
  }

  return pages.length > 0 ? pages : [['']];
}

function pageContent(lines: string[]): string {
  const commands = [
    'BT',
    `/F1 ${FONT_SIZE} Tf`,
    `${LEADING} TL`,
    `1 0 0 1 ${MARGIN} ${START_Y.toFixed(2)} Tm`
  ];

  lines.forEach((line, index) => {
    if (index > 0) {
      commands.push('T*');
    }
    commands.push(`(${encodeWinAnsi(line)}) Tj`);
  });
  commands.push('ET');
  return commands.join('\n');
}

function measure(text: string): number {
  let units = 0;
  for (const character of text) {
    units += GLYPH_WIDTHS[character] ?? DEFAULT_GLYPH_WIDTH;
  }

  return (units / 1000) * FONT_SIZE;
}

function encodeWinAnsi(text: string): string {
  let encoded = '';
  for (const character of text) {
    const code = character.codePointAt(0) ?? 0x3f;
    let byte = WINANSI_SPECIALS[character];
    if (byte === undefined) {
      byte = code >= 0x20 && code <= 0x7e ? code : code >= 0xa0 && code <= 0xff ? code : 0x3f;
    }
    encoded += escapePdfByte(byte);
  }

  return encoded;
}

function escapePdfByte(byte: number): string {
  if (byte === 0x5c || byte === 0x28 || byte === 0x29) {
    return `\\${String.fromCharCode(byte)}`;
  }

  return String.fromCharCode(byte);
}

function encodePdf(pageStreams: readonly string[], title: string): Uint8Array {
  const objects: string[] = [];
  const pageObjectNumbers = pageStreams.map((_, index) => 4 + index * 2);
  const kids = pageObjectNumbers.map((number) => `${number} 0 R`).join(' ');
  const infoId = 4 + pageStreams.length * 2;

  objects[1] = '<< /Type /Catalog /Pages 2 0 R >>';
  objects[2] = `<< /Type /Pages /Kids [${kids}] /Count ${pageStreams.length} >>`;
  objects[3] = '<< /Type /Font /Subtype /Type1 /BaseFont /Times-Roman /Encoding /WinAnsiEncoding >>';

  pageStreams.forEach((stream, index) => {
    const pageId = pageObjectNumbers[index];
    const contentId = pageId + 1;
    objects[pageId] =
      `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${PAGE_WIDTH} ${PAGE_HEIGHT}] /Contents ${contentId} 0 R /Resources << /Font << /F1 3 0 R >> >> >>`;
    objects[contentId] = `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`;
  });
  objects[infoId] = `<< /Title ${pdfUtf16Literal(title)} /Creator (ApplyVault) >>`;

  let body = '%PDF-1.4\n';
  const offsets = [0];
  for (let id = 1; id < objects.length; id += 1) {
    offsets[id] = body.length;
    body += `${id} 0 obj\n${objects[id]}\nendobj\n`;
  }

  const startxref = body.length;
  let xref = `xref\n0 ${objects.length}\n0000000000 65535 f \n`;
  for (let id = 1; id < objects.length; id += 1) {
    xref += `${String(offsets[id]).padStart(10, '0')} 00000 n \n`;
  }

  body += xref;
  body += `trailer\n<< /Size ${objects.length} /Root 1 0 R /Info ${infoId} 0 R >>\nstartxref\n${startxref}\n%%EOF\n`;

  const bytes = new Uint8Array(body.length);
  for (let index = 0; index < body.length; index += 1) {
    bytes[index] = body.charCodeAt(index) & 0xff;
  }

  return bytes;
}

function pdfUtf16Literal(value: string): string {
  const bytes = [0xfe, 0xff];
  for (const character of value) {
    const code = character.codePointAt(0) ?? 0x3f;
    bytes.push((code >> 8) & 0xff, code & 0xff);
  }

  let literal = '(';
  for (const byte of bytes) {
    if (byte === 0x5c || byte === 0x28 || byte === 0x29) {
      literal += `\\${String.fromCharCode(byte)}`;
    } else {
      literal += String.fromCharCode(byte);
    }
  }
  literal += ')';
  return literal;
}
