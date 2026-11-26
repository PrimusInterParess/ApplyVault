import { CommonModule } from '@angular/common';
import { Component, computed, HostListener, inject, OnInit, signal, ViewEncapsulation } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PreparationDiscoveryComponent } from '../../components/preparation-discovery/preparation-discovery.component';
import { renderPreparationMarkdown } from '../../utils/preparation-markdown';
import { InterviewPrepFacade } from '../../data-access/interview-prep.facade';
import { MAX_PREPARATION_DAYS, PreparationPack, PreparationReadinessKey } from '../../models/interview-prep.model';

@Component({
  selector: 'app-interview-prep-page',
  imports: [CommonModule, FormsModule, RouterLink, PreparationDiscoveryComponent],
  providers: [InterviewPrepFacade],
  templateUrl: './interview-prep-page.component.html',
  styleUrl: './interview-prep-page.component.scss',
  encapsulation: ViewEncapsulation.None
})
export class InterviewPrepPageComponent implements OnInit {
  readonly facade = inject(InterviewPrepFacade);
  readonly maxPreparationDays = MAX_PREPARATION_DAYS;
  private readonly route = inject(ActivatedRoute);
  readonly inputsOpen = signal(true);
  readonly exportError = signal<string | null>(null);
  readonly readiness: readonly { key: PreparationReadinessKey; label: string }[] = [
    { key: 'readDocumentIds', label: 'Read' },
    { key: 'explainedDocumentIds', label: 'Explained without notes' },
    { key: 'demonstratedDocumentIds', label: 'Demonstrated' },
    { key: 'adaptedDocumentIds', label: 'Adapted to a changed requirement' }
  ];
  readonly markdown = computed(() => renderPreparationMarkdown(
    this.facade.document()?.markdown ?? this.facade.selected()?.content?.overviewMarkdown ?? ''));


  ngOnInit(): void { this.reload(); }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.facade.unsaved() || this.facade.generationInProgress()) event.preventDefault();
  }

  reload(): void {
    if (!this.allowDiscard()) return;
    this.facade.load(this.route.snapshot.queryParamMap.get('jobId'),
      this.facade.selected()?.id ?? this.route.snapshot.queryParamMap.get('packId'));
  }

  selectPack(pack: PreparationPack): void {
    if (!this.facade.busy() && this.allowDiscard()) this.facade.select(pack);
  }

  onJobChange(event: Event): void {
    const select = event.target;
    if (!(select instanceof HTMLSelectElement)) return;
    if (!this.facade.busy() && this.allowDiscard()) this.facade.setJob(select.value);
    else select.value = this.facade.input().scrapeResultId ?? '';
  }

  generate(): void {
    if (this.facade.selected() && !window.confirm('Replace the saved pack with a new outline and reset its answers and study progress using these inputs?')) return;
    this.facade.generate();
  }

  remove(): void {
    if (window.confirm('Delete this preparation pack and its saved progress?')) this.facade.remove();
  }

  openDocument(id: string | null): void {
    if (this.facade.busy()) return;
    this.facade.documentId.set(id);
    if (id) {
      const document = this.facade.document();
      const questions = this.facade.selected()?.discovery?.documents.find(d => d.id === id)?.questions ?? [];
      if ((document?.isGenerated === false && !questions.length) || this.facade.questionsNeedRefresh(id))
        this.facade.prepareDocument(id);
    }
  }

  followDocumentLink(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof Element)) return;
    const anchor = target.closest('a');
    const href = anchor?.getAttribute('href');
    if (!href || !href.endsWith('.md')) return;
    const id = href.slice(0, -3);
    if (this.facade.selected()?.content?.documents.some(d => d.id === id)) {
      event.preventDefault(); this.openDocument(id);
    }
  }

  documentTitle(id: string): string {
    return this.facade.selected()?.content?.documents.find(d => d.id === id)?.title ?? id;
  }

  download(): void {
    const pack = this.facade.selected();
    if (!pack?.content) return;
    const blob = new Blob([this.packMarkdown(pack)], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = window.document.createElement('a');
    anchor.href = url; anchor.download = 'interview-preparation-pack.md'; anchor.click();
    URL.revokeObjectURL(url);
  }

  async copy(): Promise<void> {
    const pack = this.facade.selected();
    if (!pack?.content) return;
    try {
      await navigator.clipboard.writeText(this.facade.document()?.markdown ?? this.packMarkdown(pack));
      this.facade.notice.set('Copied to clipboard.'); this.exportError.set(null);
    } catch { this.exportError.set('Clipboard access failed. Use Download pack instead.'); }
  }

  private packMarkdown(pack: PreparationPack): string {
    const content = pack.content!;
    const progress = this.facade.progress();
    return [
      `# ${content.title}`, pack.isDemo ? '**Demo content**' : '', content.overviewMarkdown,
      content.unknowns.length ? `## Unknowns and assumptions\n\n${content.unknowns.map(u => `- ${u}`).join('\n')}` : '',
      '## Topic map', ...content.topics.map(t => `### ${t.name}\n\n${t.relevance}\n\nEvidence: ${t.evidence}\n\n[Study document](${t.documentId}.md)`),
      '## Study plan', ...content.studyPlan.map(s => `- [${progress.completedDays.includes(s.day) ? 'x' : ' '}] Day ${s.day} (${s.minutes} minutes): ${s.activity}`),
      ...content.documents.map(d => `<a id="${d.id}"></a>\n\n# ${d.title}\n\n${d.purpose}\n\n${d.isGenerated === false ? "Not written yet. Complete this document’s clarification step in ApplyVault." : d.markdown}`),
      '## Readiness', ...content.documents.map(d => `### ${d.title}\n\n${this.readiness.map(r => `- [${progress[r.key].includes(d.id) ? 'x' : ' '}] ${r.label}`).join('\n')}`),
      '## Review log', progress.reviewLog || 'Record the weakness, correction, evidence, and next practice task.'
    ].filter(Boolean).join('\n\n').replace(/\]\(([a-z][a-z0-9-]*)\.md\)/g, '](#$1)');
  }

  canLeave(): boolean {
    if (this.facade.generationInProgress())
      return window.confirm('Preparation is still being generated. Leaving cancels the request and its unfinished result will not be saved. Leave this page?');
    return this.allowDiscard();
  }

  private allowDiscard(): boolean {
    return !this.facade.unsaved() || window.confirm('Leave without saving your clarification answers, study progress, or review log?');
  }
}
