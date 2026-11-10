import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CvStructuredEntry, CvStructuredSection } from '../../models/cv-structured.model';
import {
  CV_A4_HEIGHT_PX,
  CV_PAGE_GAP_PX,
  cvBlockOverflowsPage
} from '../../utils/cv-canvas-pages.util';
import {
  applyCvTemplateInlineEdit
} from '../../utils/cv-template-inline-edit.util';
import { CvExportTemplatePreviewComponent } from './cv-export-template-preview.component';

function repaginate(fixture: ComponentFixture<CvExportTemplatePreviewComponent>): void {
  (
    fixture.componentInstance as unknown as {
      paginateSheets(): void;
    }
  ).paginateSheets();
}

function sheetTop(page: HTMLElement, element: HTMLElement): number {
  let top = 0;
  let current: HTMLElement | null = element;

  while (current && current !== page) {
    top += current.offsetTop;
    const parent: Element | null = current.offsetParent;
    if (!(parent instanceof HTMLElement) || !page.contains(parent)) {
      break;
    }
    current = parent;
  }

  return top;
}

function sheetIndex(top: number): number {
  return Math.floor(Math.max(0, top) / (CV_A4_HEIGHT_PX + CV_PAGE_GAP_PX));
}

describe('CvExportTemplatePreviewComponent editable canvas', () => {
  let fixture: ComponentFixture<CvExportTemplatePreviewComponent>;

  const sections: CvStructuredSection[] = [
    {
      id: 'summary',
      heading: 'Summary',
      sectionType: 'Summary',
      sortOrder: 1,
      entries: [
        {
          id: 'summary-entry',
          title: '',
          subtitle: null,
          dateRange: null,
          summary: 'Product engineer',
          bullets: [],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        }
      ]
    },
    {
      id: 'experience',
      heading: 'Experience',
      sectionType: 'Experience',
      sortOrder: 2,
      entries: []
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CvExportTemplatePreviewComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CvExportTemplatePreviewComponent);
    fixture.componentRef.setInput('sections', sections);
    fixture.componentRef.setInput('editable', true);
    fixture.componentRef.setInput('profilePhotoUrl', 'https://example.test/photo.jpg');
    fixture.detectChanges();
  });

  it('keeps the editable sheet at least one A4 page tall', () => {
    const page = (fixture.nativeElement as HTMLElement).querySelector('.cv-export-preview__page');
    expect(page).not.toBeNull();
    expect(getComputedStyle(page as HTMLElement).minHeight).toBe('1123px');
    expect(getComputedStyle(page as HTMLElement).maxHeight).toBe('none');
  });

  it('keeps an empty section’s add control available and focuses a field without a filled wash', () => {
    const root = fixture.nativeElement as HTMLElement;
    const empty = root.querySelector('#cv-section-experience');
    expect(empty?.classList.contains('cv-export-preview__section--empty')).toBeTrue();
    expect(empty?.querySelector('.cv-export-preview__add-entry')?.textContent).toContain('Add');

    const field = root.querySelector('.cv-export-preview__entry-summary') as HTMLElement;
    field.focus();
    const focused = getComputedStyle(field);
    expect(focused.outlineStyle).toBe('solid');
    expect(focused.outlineWidth).toBe('1px');
    expect(focused.backgroundColor).toBe('rgba(0, 0, 0, 0)');
  });

  it('offers a change overlay when a photo is already on the page', () => {
    const change = (fixture.nativeElement as HTMLElement).querySelector('.cv-export-preview__photo-change');
    expect(change?.textContent?.trim()).toBe('Change');
  });
});

describe('CvExportTemplatePreviewComponent section visibility', () => {
  let fixture: ComponentFixture<CvExportTemplatePreviewComponent>;

  function entry(id: string, title: string, summary = ''): CvStructuredEntry {
    return {
      id,
      title,
      subtitle: null,
      dateRange: null,
      summary,
      bullets: [],
      techStack: '',
      fields: {},
      source: 'Manual',
      sourceSummaryId: null,
      sortOrder: 0
    };
  }

  const sections: CvStructuredSection[] = [
    {
      id: 'contact',
      heading: 'Contact',
      sectionType: 'Contact',
      sortOrder: 0,
      includedOnTemplate: true,
      entries: [{ ...entry('name', 'Name'), subtitle: 'Ada Lovelace' }]
    },
    {
      id: 'summary',
      heading: 'Summary',
      sectionType: 'Summary',
      sortOrder: 1,
      includedOnTemplate: true,
      entries: [entry('summary-entry', '', 'Product engineer')]
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CvExportTemplatePreviewComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CvExportTemplatePreviewComponent);
    fixture.componentRef.setInput('sections', sections);
    fixture.componentRef.setInput('editable', true);
    fixture.detectChanges();
  });

  it('hides an omitted Section from the canvas', () => {
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('.cv-export-preview__section-visibility')).toBeNull();
    expect(root.querySelector('#cv-section-summary')).not.toBeNull();

    const next = applyCvTemplateInlineEdit(sections, {
      kind: 'sectionVisibility',
      sectionId: 'summary',
      includedOnTemplate: false
    });
    fixture.componentRef.setInput('sections', next);
    fixture.detectChanges();

    expect(root.querySelector('#cv-section-summary')).toBeNull();
    expect(root.textContent).toContain('Ada Lovelace');
  });

  it('gives Contact no include/omit switch on the paper', () => {
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[aria-label="Omit Contact from CV"]')).toBeNull();
    expect(root.querySelector('h2.cv-export-preview__section-title')?.textContent).not.toContain('Contact');
  });
});

describe('CvExportTemplatePreviewComponent sheet placement', () => {
  let fixture: ComponentFixture<CvExportTemplatePreviewComponent>;

  function entry(
    id: string,
    title: string,
    bullets: string[] = [],
    summary = ''
  ): CvStructuredEntry {
    return {
      id,
      title,
      subtitle: null,
      dateRange: null,
      summary,
      bullets,
      techStack: '',
      fields: {},
      source: 'Manual',
      sourceSummaryId: null,
      sortOrder: 0
    };
  }

  function section(id: string, entries: CvStructuredEntry[]): CvStructuredSection {
    return {
      id,
      heading: 'Experience',
      sectionType: 'Experience',
      sortOrder: 1,
      includedOnTemplate: true,
      entries
    };
  }

  async function setup(sections: CvStructuredSection[]): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CvExportTemplatePreviewComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CvExportTemplatePreviewComponent);
    fixture.componentRef.setInput('sections', sections);
    fixture.componentRef.setInput('editable', true);
    fixture.detectChanges();
  }

  function pinToSheetStart(): { page: HTMLElement; main: HTMLElement } {
    const root = fixture.nativeElement as HTMLElement;
    const page = root.querySelector('.cv-export-preview__page') as HTMLElement;
    const header = page.querySelector('.cv-export-preview__minimal-header') as HTMLElement;
    const main = page.querySelector('.cv-export-preview__main') as HTMLElement;
    header.style.display = 'none';
    main.style.paddingTop = '0';
    return { page, main };
  }

  it('tightens the type scale so content that can fit stays on one sheet', async () => {
    await setup([section('experience', [entry('role', 'Engineer', [], 'Shipped the desk')])]);
    const { page, main } = pinToSheetStart();
    const role = main.querySelector('.cv-export-preview__entry') as HTMLElement;

    role.style.minHeight = 'calc(1250px * var(--cv-canvas-scale, 1))';
    repaginate(fixture);

    expect(page.style.getPropertyValue('--cv-canvas-scale')).toBe('0.86');
    expect(role.style.marginTop).toBe('');
    expect(page.scrollHeight).toBeLessThanOrEqual(CV_A4_HEIGHT_PX + 1);
  });

  it('moves one Entry with its heading when that Entry does not fit', async () => {
    await setup([section('experience', [entry('role', 'Engineer', [], 'Shipped the desk')])]);
    const { main } = pinToSheetStart();
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const role = block.querySelector('.cv-export-preview__entry') as HTMLElement;

    main.style.paddingTop = '200px';
    role.style.minHeight = '1000px';
    repaginate(fixture);

    expect(block.style.marginTop).not.toBe('');
    expect(role.style.marginTop).toBe('');
    expect(block.contains(role)).toBeTrue();
    expect(cvBlockOverflowsPage(sheetTop(block.closest('.cv-export-preview__page') as HTMLElement, role), role.offsetHeight)).toBeFalse();
  });

  it('continues a Section between Entries and keeps each Entry whole', async () => {
    await setup([
      section('experience', [
        entry('first', 'First role', ['Stays on this page']),
        entry('second', 'Second role', ['Moves as a whole entry'])
      ])
    ]);
    const { page, main } = pinToSheetStart();
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const roles = [...block.querySelectorAll('.cv-export-preview__entry')] as HTMLElement[];

    main.style.paddingTop = '200px';
    roles[0].style.minHeight = '400px';
    roles[1].style.minHeight = '500px';
    repaginate(fixture);

    expect(block.style.marginTop).toBe('');
    expect(roles[0].style.marginTop).toBe('');
    expect(roles[1].style.marginTop).not.toBe('');
    expect(roles[0].querySelector('li')?.style.marginTop ?? '').toBe('');
    expect(roles[1].querySelector('li')?.style.marginTop ?? '').toBe('');
    expect(cvBlockOverflowsPage(sheetTop(page, roles[0]), roles[0].offsetHeight)).toBeFalse();
    expect(cvBlockOverflowsPage(sheetTop(page, roles[1]), roles[1].offsetHeight)).toBeFalse();
    expect(sheetIndex(sheetTop(page, roles[0]))).toBe(0);
    expect(sheetIndex(sheetTop(page, roles[1]))).toBe(1);
  });

  it('starts a continued Entry at the same top inset as page 1', async () => {
    await setup([
      section('experience', [
        entry('first', 'First role', ['Stays on this page']),
        entry('second', 'Second role', ['Moves as a whole entry'])
      ])
    ]);
    const { page, main } = pinToSheetStart();
    const pad = 48;
    page.style.setProperty('--cv-sheet-pad-top', `${pad}px`);
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const roles = [...block.querySelectorAll('.cv-export-preview__entry')] as HTMLElement[];

    main.style.paddingTop = '200px';
    roles[0].style.minHeight = '400px';
    roles[1].style.minHeight = '500px';
    repaginate(fixture);

    const page2Start = CV_A4_HEIGHT_PX + CV_PAGE_GAP_PX;
    const continuedTop = sheetTop(page, roles[1]);
    expect(continuedTop).toBeGreaterThanOrEqual(page2Start + pad - 1);
    expect(continuedTop).toBeLessThan(page2Start + pad + 2);
    expect(cvBlockOverflowsPage(continuedTop, roles[1].offsetHeight)).toBeFalse();
  });

  it('keeps the normal rhythm when an Entry moves to the next sheet', async () => {
    await setup([
      section('experience', [
        entry('first', 'First role', ['Stays']),
        entry('second', 'Second role', ['Moves'])
      ])
    ]);
    const { main } = pinToSheetStart();
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const heading = block.querySelector('.cv-export-preview__section-title') as HTMLElement;
    const roles = [...block.querySelectorAll('.cv-export-preview__entry')] as HTMLElement[];

    main.style.paddingTop = '200px';
    roles[0].style.minHeight = '400px';
    roles[1].style.minHeight = '700px';
    repaginate(fixture);

    expect(heading.style.marginBottom).toBe('');
    expect(roles[0].style.marginBottom).toBe('');
    expect(roles[1].style.marginTop).not.toBe('');
    expect(sheetIndex(sheetTop(main.closest('.cv-export-preview__page') as HTMLElement, roles[1]))).toBe(1);
  });

  it('does not move a Section taller than one page as a whole', async () => {
    await setup([
      section('experience', [
        entry('first', 'First role', ['Kept the first entry']),
        entry('second', 'Second role', ['Moved the second entry'])
      ])
    ]);
    const { page, main } = pinToSheetStart();
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const roles = [...block.querySelectorAll('.cv-export-preview__entry')] as HTMLElement[];

    roles[0].style.minHeight = '900px';
    roles[1].style.minHeight = '400px';
    repaginate(fixture);

    expect(block.offsetHeight).toBeGreaterThan(CV_A4_HEIGHT_PX);
    expect(block.style.marginTop).toBe('');
    expect(roles[0].style.marginTop).toBe('');
    expect(roles[1].style.marginTop).not.toBe('');
    expect(cvBlockOverflowsPage(sheetTop(page, roles[1]), roles[1].offsetHeight)).toBeFalse();
    roles[1].querySelectorAll('li').forEach((item) => {
      expect((item as HTMLElement).style.marginTop).toBe('');
    });
  });

  it('keeps a tall Section heading with the next Entry', async () => {
    await setup([
      section('experience', [
        entry('first', 'First role', [], 'Short lead'),
        entry('second', 'Second role', [], 'Later entry')
      ])
    ]);
    const { page, main } = pinToSheetStart();
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const heading = block.querySelector('.cv-export-preview__section-title') as HTMLElement;
    const roles = [...block.querySelectorAll('.cv-export-preview__entry')] as HTMLElement[];

    main.style.paddingTop = '900px';
    roles[0].style.minHeight = '400px';
    roles[1].style.minHeight = '800px';
    repaginate(fixture);

    expect(block.offsetHeight).toBeGreaterThan(CV_A4_HEIGHT_PX);
    expect(block.style.marginTop).not.toBe('');
    expect(roles[0].style.marginTop).toBe('');
    expect(roles[1].style.marginTop).not.toBe('');
    expect(sheetIndex(sheetTop(page, heading))).toBe(sheetIndex(sheetTop(page, roles[0])));
    expect(cvBlockOverflowsPage(sheetTop(page, roles[0]), roles[0].offsetHeight)).toBeFalse();
  });

  it('keeps each bullet whole when an Entry is taller than one page', async () => {
    const role = entry('role', 'Engineer', ['First bullet', 'Second bullet', 'Third bullet', 'Fourth bullet'], 'Built the desk');
    role.subtitle = 'ApplyVault';
    await setup([section('experience', [role])]);
    const { page, main } = pinToSheetStart();
    const block = main.querySelector('#cv-section-experience') as HTMLElement;
    const article = block.querySelector('.cv-export-preview__entry') as HTMLElement;
    const header = article.querySelector('.cv-export-preview__entry-header') as HTMLElement;
    const subtitle = article.querySelector('.cv-export-preview__entry-subtitle') as HTMLElement;
    const summary = article.querySelector('.cv-export-preview__entry-summary') as HTMLElement;
    const bullets = [...article.querySelectorAll('li')] as HTMLElement[];

    bullets.forEach((item) => {
      item.style.minHeight = '400px';
    });
    repaginate(fixture);

    expect(article.offsetHeight).toBeGreaterThan(CV_A4_HEIGHT_PX);
    expect(block.style.marginTop).toBe('');
    expect(article.style.marginTop).toBe('');
    expect(header.style.marginTop).toBe('');
    expect(subtitle.style.marginTop).toBe('');
    expect(summary.style.marginTop).toBe('');
    expect(sheetIndex(sheetTop(page, header))).toBe(sheetIndex(sheetTop(page, subtitle)));
    expect(sheetIndex(sheetTop(page, header))).toBe(sheetIndex(sheetTop(page, summary)));
    expect(bullets.some((item) => item.style.marginTop !== '')).toBeTrue();
    bullets.forEach((item) => {
      expect(item.textContent?.trim().length).toBeGreaterThan(0);
      if (item.offsetHeight <= CV_A4_HEIGHT_PX) {
        expect(cvBlockOverflowsPage(sheetTop(page, item), item.offsetHeight)).toBeFalse();
      }
    });
  });

  it('keeps an Entry header, subtitle, and summary together when that lead does not fit', async () => {
    const role = entry('role', 'Engineer', ['One', 'Two', 'Three', 'Four'], 'Built the desk');
    role.subtitle = 'ApplyVault';
    await setup([section('experience', [role])]);
    const { page, main } = pinToSheetStart();
    const article = main.querySelector('.cv-export-preview__entry') as HTMLElement;
    const header = article.querySelector('.cv-export-preview__entry-header') as HTMLElement;
    const subtitle = article.querySelector('.cv-export-preview__entry-subtitle') as HTMLElement;
    const summary = article.querySelector('.cv-export-preview__entry-summary') as HTMLElement;
    const bullets = [...article.querySelectorAll('li')] as HTMLElement[];

    main.style.paddingTop = '1000px';
    summary.style.minHeight = '180px';
    bullets.forEach((item) => {
      item.style.minHeight = '320px';
    });
    repaginate(fixture);

    const leadHeight = sheetTop(page, summary) + summary.offsetHeight - sheetTop(page, header);
    expect(article.offsetHeight).toBeGreaterThan(CV_A4_HEIGHT_PX);
    expect(article.style.marginTop).not.toBe('');
    expect(header.style.marginTop).toBe('');
    expect(subtitle.style.marginTop).toBe('');
    expect(summary.style.marginTop).toBe('');
    expect(sheetIndex(sheetTop(page, header))).toBe(sheetIndex(sheetTop(page, subtitle)));
    expect(sheetIndex(sheetTop(page, header))).toBe(sheetIndex(sheetTop(page, summary)));
    expect(cvBlockOverflowsPage(sheetTop(page, header), leadHeight)).toBeFalse();
    bullets.forEach((item) => {
      if (item.offsetHeight <= CV_A4_HEIGHT_PX) {
        expect(cvBlockOverflowsPage(sheetTop(page, item), item.offsetHeight)).toBeFalse();
      }
    });
  });
});
