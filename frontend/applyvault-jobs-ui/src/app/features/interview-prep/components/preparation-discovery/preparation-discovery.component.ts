import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InterviewPrepFacade } from '../../data-access/interview-prep.facade';
import { PreparationAnswerStatus, PreparationDocument } from '../../models/interview-prep.model';
import { renderPreparationMarkdown } from '../../utils/preparation-markdown';

@Component({
  selector: 'app-preparation-discovery',
  imports: [FormsModule],
  templateUrl: './preparation-discovery.component.html',
  styleUrl: './preparation-discovery.component.scss'
})
export class PreparationDiscoveryComponent {
  readonly facade = inject(InterviewPrepFacade);
  readonly document = input.required<PreparationDocument>();
  readonly expanded = signal(true);
  readonly questionIndex = signal(0);
  readonly helpOpenFor = signal<string | null>(null);
  readonly helpRequest = signal('');
  readonly questions = computed(() => this.facade.selected()?.discovery?.documents.find(d => d.id === this.document().id)?.questions ?? []);
  readonly question = computed(() => this.questions()[this.questionIndex()] ?? null);
  readonly answer = computed(() => this.facade.answers(this.document().id).find(a => a.questionId === this.question()?.id));
  readonly completedCount = computed(() => this.facade.answers(this.document().id).filter(a => a.status !== 'pending').length);
  readonly extraHelp = computed(() => {
    const help = this.facade.helpResult();
    return help?.documentId === this.document().id && help.questionId === this.question()?.id ? help.response : null;
  });
  readonly renderMarkdown = renderPreparationMarkdown;
  private previousDocumentId: string | null = null;
  private wasPending = false;

  constructor() {
    effect(() => {
      const document = this.document();
      if (this.previousDocumentId === document.id) {
        if (this.wasPending && document.isGenerated !== false) this.expanded.set(false);
        this.wasPending = document.isGenerated === false;
        return;
      }
      this.previousDocumentId = document.id;
      this.wasPending = document.isGenerated === false;
      this.questionIndex.set(0); this.helpOpenFor.set(null); this.helpRequest.set('');
      this.expanded.set(document.isGenerated === false);
    });
  }

  updateText(text: string): void {
    const question = this.question();
    if (!question) return;
    const status = this.answer()?.status === 'unknown' ? 'unknown' : text.trim() ? 'answered' : 'pending';
    this.facade.updateAnswer(this.document().id, question.id, { text, status });
  }

  choose(status: PreparationAnswerStatus): void {
    const question = this.question();
    if (!question) return;
    this.facade.updateAnswer(this.document().id, question.id, {
      status, text: status === 'skipped' ? '' : this.answer()?.text ?? ''
    });
  }

  changeQuestion(index: number): void {
    this.questionIndex.set(index); this.helpOpenFor.set(null); this.helpRequest.set('');
  }

  showHelp(): void {
    this.helpOpenFor.set(this.helpOpenFor() === this.question()?.id ? null : this.question()?.id ?? null);
  }

  askForHelp(request = this.helpRequest()): void {
    const question = this.question();
    if (question) this.facade.askForHelp(this.document().id, question.id, request);
  }

  useStarter(): void {
    const question = this.question();
    if (!question) return;
    if (this.answer()?.text.trim() && !window.confirm('Replace your current draft with the blank starting structure?')) return;
    this.updateText(question.answerStarter);
  }
}
