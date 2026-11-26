import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_CONFIG } from '../../../../core/config/api.config';
import { TEST_API_BASE_URL } from '../../../../../testing/api-fixtures';
import { discoveryPack, preparationPack } from '../../testing/preparation-fixtures';
import { InterviewPrepPageComponent } from './interview-prep-page.component';

describe('InterviewPrepPageComponent', () => {
  let fixture: ComponentFixture<InterviewPrepPageComponent>;
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [InterviewPrepPageComponent], providers: [provideHttpClient(),
      provideHttpClientTesting(), provideRouter([]), { provide: API_CONFIG, useValue: { baseUrl: TEST_API_BASE_URL } }] }).compileComponents();
    fixture = TestBed.createComponent(InterviewPrepPageComponent); http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });
  afterEach(() => http.verify());

  function load(hasCv: boolean, withPack = false): void {
    http.expectOne(`${TEST_API_BASE_URL}/interview-prep/context`).flush({ hasStructuredCv: hasCv, projects: [] });
    http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs`).flush(withPack ? [preparationPack()] : []);
    http.expectOne(`${TEST_API_BASE_URL}/scrape-results`).flush([]);
    fixture.detectChanges();
  }

  it('explains the CV requirement and disables generation without a CV', () => {
    load(false);
    expect(fixture.nativeElement.textContent).toContain('Start with your CV');
    expect(fixture.componentInstance.facade.canGenerate()).toBeFalse();
  });

  it('opens linked teaching documents within the reader', () => {
    load(true, true);
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('.prep-markdown a');
    link.click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.prep-reader').textContent).toContain('Follow the constraint');
    expect(fixture.componentInstance.facade.documentId()).toBe('foundations');
    expect(fixture.nativeElement.textContent).not.toContain('Submit answer');
  });

  it('sanitizes generated Markdown before rendering', () => {
    load(true, true);
    const pack = preparationPack();
    fixture.componentInstance.facade.select({ ...pack, content: { ...pack.content!, overviewMarkdown: '<img src=x onerror="alert(1)"><script>alert(1)</script>' } });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.prep-markdown script')).toBeNull();
    expect(fixture.nativeElement.querySelector('.prep-markdown img')).toBeNull();
  });
  it('exports the whole pack with working document anchors and the current review log', async () => {
    load(true, true);
    fixture.componentInstance.facade.setReviewLog('Unsaved observation from my latest attempt.');
    let exported: Blob | undefined;
    spyOn(URL, 'createObjectURL').and.callFake(value => {
      if (value instanceof Blob) exported = value;
      return 'blob:preparation-preview';
    });
    spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click');
    fixture.componentInstance.download();
    const text = await exported!.text();
    expect(text).toContain('](#foundations)');
    expect(text).toContain('<a id="foundations"></a>');
    expect(text).toContain('Unsaved observation from my latest attempt.');
    expect(text).not.toContain('](foundations.md)');
  });

  it('protects an unsaved review log when leaving the route', () => {
    load(true, true);
    fixture.componentInstance.facade.setReviewLog('My next weakness.');
    spyOn(window, 'confirm').and.returnValue(false);
    expect(fixture.componentInstance.canLeave()).toBeFalse();
  });

  function button(text: string): HTMLButtonElement {
    const root: HTMLElement = fixture.nativeElement;
    return Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === text)!;
  }

  function openDiscovery(): void {
    load(true);
    fixture.componentInstance.facade.select(discoveryPack());
    fixture.componentInstance.openDocument('foundations');
    fixture.detectChanges();
  }

  it('shows one clarification at a time and explains uncertainty without inventing experience', () => {
    openDiscovery();
    expect(fixture.nativeElement.textContent).toContain('Where do you need a clearer explanation?');
    expect(fixture.nativeElement.textContent).not.toContain('Which part did you personally handle?');
    expect(fixture.nativeElement.querySelector('.prep-readiness')).toBeNull();
    button("I don't know").click(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('foundations and a worked example');
    button('Next question').click(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Which part did you personally handle?');
    button("I don't know").click(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('without inventing your history');
    expect(fixture.componentInstance.facade.canWrite('foundations')).toBeTrue();
  });

  it('offers guidance and follow-up help without filling in a personal answer', () => {
    openDiscovery();
    button('Help me answer').click(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Starting structure');
    expect(fixture.nativeElement.textContent).toContain('Hypothetical example');
    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector<HTMLTextAreaElement>('#foundations-needs-answer')!.value).toBe('');
    button('Explain more simply').click();
    const help = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/questions/foundations-needs/help`);
    expect(help.request.body.draftAnswer).toBe('');
    help.flush({ markdown: 'Try one small task and describe where you get stuck.', isDemo: false });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Try one small task');
    expect(root.querySelector<HTMLTextAreaElement>('#foundations-needs-answer')!.value).toBe('');
    expect(fixture.componentInstance.facade.selected()?.discovery?.answers).toEqual([]);
  });

  it('persists unknown and skipped answers before writing this document', () => {
    openDiscovery();
    button("I don't know").click(); fixture.detectChanges();
    button('Next question').click(); fixture.detectChanges();
    button('Skip').click(); fixture.detectChanges();
    button('Save answers and write this document').click();
    const answers = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/answers`);
    expect(answers.request.body.answers.map((a: { status: string }) => a.status)).toEqual(['unknown', 'skipped']);
    const pack = discoveryPack();
    const saved = { ...pack, updatedAt: '2026-11-26T11:00:00Z', discovery: {
      ...pack.discovery!, answers: answers.request.body.answers.map((a: object) => ({ ...a, documentId: 'foundations' })), contextRevision: 1
    } };
    answers.flush(saved);
    const write = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/generate`);
    expect(write.request.body.expectedUpdatedAt).toBe(saved.updatedAt);
    write.flush({ ...saved, content: preparationPack().content }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Follow the constraint');
    expect(fixture.nativeElement.querySelector('.prep-readiness')).not.toBeNull();
  });

  it('protects unsaved discovery context when leaving the route', () => {
    openDiscovery();
    button("I don't know").click(); fixture.detectChanges();
    spyOn(window, 'confirm').and.returnValue(false);
    expect(fixture.componentInstance.canLeave()).toBeFalse();
  });

  it('enables planning when the form has a role, level, and 40 preparation days', async () => {
    load(true); await fixture.whenStable(); fixture.detectChanges();
    const root: HTMLElement = fixture.nativeElement;
    for (const [name, value] of [['role', 'Full-stack developer'], ['level', 'Senior'], ['days', '40']]) {
      const input = root.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
      input.value = value; input.dispatchEvent(new Event('input'));
    }
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(button('Plan preparation documents').disabled).toBeFalse();
  });

  it('explains why planning is disabled when the preparation time is invalid', () => {
    load(true);
    fixture.componentInstance.facade.patchInput({ targetRole: 'Developer', targetLevel: 'Senior', preparationDays: 0 });
    fixture.detectChanges();
    expect(button('Plan preparation documents').disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Choose 1–90 whole preparation days.');
  });

  it('warns before leaving or reloading while a plan is still being generated', () => {
    load(true);
    fixture.componentInstance.facade.busy.set('generating');
    const confirmation = spyOn(window, 'confirm').and.returnValue(false);
    expect(fixture.componentInstance.canLeave()).toBeFalse();
    expect(confirmation).toHaveBeenCalledWith(jasmine.stringMatching('Leaving cancels the request'));
    const event = new Event('beforeunload', { cancelable: true });
    fixture.componentInstance.beforeUnload(event);
    expect(event.defaultPrevented).toBeTrue();
    fixture.componentInstance.facade.busy.set(null);
  });

  it('prepares deferred questions when an unwritten document is opened', () => {
    load(true);
    const pack = discoveryPack();
    fixture.componentInstance.facade.select({ ...pack, discovery: {
      ...pack.discovery!, documents: pack.discovery!.documents.map(d => ({ ...d, questions: [] }))
    } });
    fixture.componentInstance.openDocument('foundations');
    const request = http.expectOne(`${TEST_API_BASE_URL}/interview-prep/packs/pack-1/documents/foundations/discovery`);
    expect(request.request.body.expectedUpdatedAt).toBe(pack.updatedAt);
    request.flush(pack); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Where do you need a clearer explanation?');
    expect(fixture.componentInstance.facade.canWrite('foundations')).toBeFalse();
  });

});
