import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { InterviewPrepFacade } from '../../data-access/interview-prep.facade';
import {
  INTERVIEW_PREP_EXPERIENCE_TYPES,
  INTERVIEW_PREP_LANGUAGES,
  INTERVIEW_PREP_MARKETS,
  INTERVIEW_PREP_MODES,
  INTERVIEW_PREP_PERSONAS,
  InterviewPrepExperienceType,
  InterviewPrepLanguage,
  InterviewPrepMarket,
  InterviewPrepMode,
  InterviewPrepPersona,
  InterviewPrepSessionStatus,
  InterviewPrepSessionSummary,
  InterviewPrepTurn
} from '../../models/interview-prep.model';

@Component({
  selector: 'app-interview-prep-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './interview-prep-page.component.html',
  styleUrl: './interview-prep-page.component.scss'
})
export class InterviewPrepPageComponent implements OnInit {
  protected readonly facade = inject(InterviewPrepFacade);
  private readonly route = inject(ActivatedRoute);

  protected readonly answerDraft = signal('');
  protected readonly retryDraft = signal('');

  protected readonly modes = INTERVIEW_PREP_MODES;
  protected readonly personas = INTERVIEW_PREP_PERSONAS;
  protected readonly languages = INTERVIEW_PREP_LANGUAGES;
  protected readonly markets = INTERVIEW_PREP_MARKETS;
  protected readonly experienceTypes = INTERVIEW_PREP_EXPERIENCE_TYPES;

  ngOnInit(): void {
    this.facade.loadCvGate();
    this.facade.loadJobOptions();
    this.facade.loadHistory();

    const jobId = this.route.snapshot.queryParamMap.get('jobId');
    this.facade.applyJobIdFromQuery(jobId);

    const sessionId = this.route.snapshot.queryParamMap.get('sessionId');
    if (sessionId) {
      this.facade.loadSession(sessionId);
    }
  }

  protected selectMode(mode: InterviewPrepMode, event: Event): void {
    event.preventDefault();
    this.facade.setMode(mode);
  }

  protected selectPersona(persona: InterviewPrepPersona, event: Event): void {
    event.preventDefault();
    this.facade.setPersona(persona);
  }

  protected isPersonaEnabled(persona: InterviewPrepPersona): boolean {
    return this.facade.availablePersonas().includes(persona);
  }

  protected selectLanguage(language: InterviewPrepLanguage, event: Event): void {
    event.preventDefault();
    this.facade.setLanguage(language);
  }

  protected selectMarket(market: InterviewPrepMarket, event: Event): void {
    event.preventDefault();
    this.facade.setMarket(market);
  }

  protected selectExperienceType(type: InterviewPrepExperienceType, event: Event): void {
    event.preventDefault();
    this.facade.setExperienceType(type);
  }

  protected onJobChange(value: string): void {
    this.facade.setSelectedJob(value || null);
  }

  protected submitAnswer(): void {
    const text = this.answerDraft();
    if (!text.trim()) {
      return;
    }
    this.facade.submitAnswer(text);
    this.answerDraft.set('');
  }

  protected openHistorySession(item: InterviewPrepSessionSummary, event: Event): void {
    event.preventDefault();
    this.facade.loadSession(item.id);
  }

  protected confirmDeleteSession(item: InterviewPrepSessionSummary, event: Event): void {
    event.preventDefault();
    if (window.confirm('Delete this practice session?')) {
      this.facade.deleteHistorySession(item.id);
    }
  }

  protected turnRoleLabel(turn: InterviewPrepTurn): string {
    if (turn.role === 'interviewer') {
      return 'Interviewer';
    }
    if (turn.role === 'candidate') {
      return 'You';
    }
    if (turn.role === 'coach') {
      return 'Coach';
    }
    return 'System';
  }

  protected historyJobLabel(item: InterviewPrepSessionSummary): string | null {
    if (item.jobTitle && item.companyName) {
      return `${item.jobTitle} · ${item.companyName}`;
    }
    return item.jobTitle ?? item.companyName ?? null;
  }

  protected needsContinue(): boolean {
    const status = this.facade.sessionDetail()?.status;
    return status === 'created' || status === 'ready';
  }

  protected experienceDescription(): string {
    return (
      this.experienceTypes.find((t) => t.id === this.facade.experienceType())?.description ?? ''
    );
  }

  protected modeLabel(mode: InterviewPrepMode | string | null | undefined): string {
    return this.modes.find((m) => m.id === mode)?.label ?? mode ?? '';
  }

  protected personaLabel(persona: InterviewPrepPersona | string | null | undefined): string {
    return this.personas.find((p) => p.id === persona)?.label ?? persona ?? '';
  }

  protected sessionStatusLabel(status: InterviewPrepSessionStatus | string | null | undefined): string {
    const labels: Record<string, string> = {
      created: 'Created',
      preparing: 'Preparing',
      ready: 'Ready',
      inProgress: 'In progress',
      paused: 'Paused',
      completing: 'Completing',
      completed: 'Completed',
      cancelled: 'Cancelled',
      failed: 'Failed'
    };
    if (!status) {
      return '';
    }
    return labels[status] ?? status;
  }

  protected languageLabel(language: string | null | undefined): string {
    return this.languages.find((l) => l.id === language)?.label ?? language ?? '';
  }

  protected showTurnLanguage(turn: InterviewPrepTurn): boolean {
    if (!turn.language) {
      return false;
    }
    const sessionLang = this.facade.sessionDetail()?.language ?? this.facade.language();
    return turn.language !== sessionLang;
  }

  protected submitRetry(): void {
    this.facade.submitCoachingRetry(this.retryDraft());
    this.retryDraft.set('');
  }
}
