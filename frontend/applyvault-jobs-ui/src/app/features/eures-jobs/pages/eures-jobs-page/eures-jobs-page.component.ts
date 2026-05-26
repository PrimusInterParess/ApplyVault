import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import { EuresJobsFacade } from '../../data-access/eures-jobs.facade';
import { EURES_KEYWORD_SUGGESTION_GROUPS } from '../../models/eures-keyword-suggestions';

@Component({
  selector: 'app-eures-jobs-page',
  standalone: true,
  imports: [CommonModule, RouterLink, SafeHtmlPipe],
  providers: [EuresJobsFacade],
  templateUrl: './eures-jobs-page.component.html',
  styleUrl: './eures-jobs-page.component.scss'
})
export class EuresJobsPageComponent implements OnInit {
  readonly facade = inject(EuresJobsFacade);
  readonly keywordSuggestionGroups = EURES_KEYWORD_SUGGESTION_GROUPS;
  readonly draftKeyword = signal('');

  protected asValue(event: Event): string {
    return (event.target as HTMLInputElement | null)?.value ?? '';
  }

  protected updateDraftKeyword(event: Event): void {
    this.draftKeyword.set(this.asValue(event));
  }

  protected toggleSuggestion(keyword: string): void {
    this.facade.toggleKeyword(keyword);
  }

  protected isActiveSuggestion(keyword: string): boolean {
    return this.facade.isKeywordSelected(keyword);
  }

  protected runSearch(): void {
    if (!this.canSearch()) {
      return;
    }

    this.facade.search(this.draftKeyword());
    this.draftKeyword.set('');
  }

  protected runSearchFromKeyboard(event: Event): void {
    event.preventDefault();
    this.runSearch();
  }

  protected canSearch(): boolean {
    return this.facade.keywords().length > 0 || this.draftKeyword().trim().length > 0;
  }

  protected removeKeyword(keyword: string): void {
    this.facade.removeKeyword(keyword);
  }

  ngOnInit(): void {
    this.facade.initialize();
  }

  protected retrySearch(): void {
    this.facade.refreshCurrentSearch();
  }

  protected detailUrl(): string | null {
    const selectedJob = this.facade.selectedJob();
    if (!selectedJob) {
      return null;
    }

    return selectedJob.applicationUrl ?? selectedJob.sourceUrl;
  }
}
