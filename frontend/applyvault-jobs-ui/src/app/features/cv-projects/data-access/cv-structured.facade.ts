import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { isRequestAborted } from '../../../core/http/is-request-aborted';
import {
  CvImprovementSuggestion,
  CvQualityEvaluation,
  CvStructuredDocument,
  CvStructuredSection,
  CvSummaryProposal,
  CvUpdateProposal
} from '../models/cv-structured.model';
import { mergeAssistStructuredUpdate } from '../utils/cv-structured-assist-merge.util';
import {
  cloneSectionsForDraft,
  createEmptyEntry,
  hydrateStructuredDocument,
  toSaveRequest
} from '../utils/cv-structured-draft.util';
import { normalizeSectionsForEditing } from '../utils/cv-structured-edit-normalizer.util';
import { createSectionOfType } from '../utils/cv-starter-entry.util';
import { CvDocumentApiService } from './cv-document-api.service';

type PendingStructuredSave = {
  sections: readonly CvStructuredSection[];
  sectionId: string | null;
  generation: number;
};

@Injectable({ providedIn: 'root' })
export class CvStructuredFacade {
  private readonly apiService = inject(CvDocumentApiService);
  private loadSubscription: Subscription | null = null;
  private saveSubscription: Subscription | null = null;
  private aiUpdateSubscription: Subscription | null = null;
  private updateProposeSubscription: Subscription | null = null;
  private suggestionsSubscription: Subscription | null = null;
  private evaluationSubscription: Subscription | null = null;
  private summaryProposeSubscription: Subscription | null = null;

  private loadGeneration = 0;
  private saveGeneration = 0;
  private pendingSave: PendingStructuredSave | null = null;

  readonly loading = signal(false);
  readonly savingSectionId = signal<string | null>(null);
  /** @deprecated Assist uses proposingUpdate; kept false for any leftover bindings. */
  readonly updatingWithAi = signal(false);
  readonly proposingUpdate = signal(false);
  readonly generatingSuggestions = signal(false);
  readonly evaluating = signal(false);
  readonly proposing = signal(false);
  readonly structured = signal<CvStructuredDocument | null>(null);
  readonly suggestions = signal<CvImprovementSuggestion[]>([]);
  /** Session-only evaluation report — never written to storage (D2). */
  readonly evaluation = signal<CvQualityEvaluation | null>(null);
  /** Session-only Summary proposal — Approve via local patch + save; never ai-update. */
  readonly summaryProposal = signal<CvSummaryProposal | null>(null);
  /** Session-only Update proposal — Approve via merge + save; never ai-update. */
  readonly updateProposal = signal<CvUpdateProposal | null>(null);
  readonly error = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly aiUpdateError = signal<string | null>(null);
  readonly suggestionError = signal<string | null>(null);
  readonly evaluationError = signal<string | null>(null);
  readonly summaryProposalError = signal<string | null>(null);
  /** Monotonic generation of the last successfully applied structured save. */
  readonly lastSuccessfulSaveGeneration = signal(0);

  readonly isSaving = computed(() => this.savingSectionId() !== null);

  load(): void {
    const generation = ++this.loadGeneration;
    this.cancelLoadSubscription();
    this.loading.set(true);
    this.error.set(null);

    this.loadSubscription = this.apiService.getStructured().subscribe({
      next: (document) => {
        if (generation !== this.loadGeneration) {
          return;
        }

        this.loading.set(false);
        this.setStructured(document);
      },
      error: (error) => {
        if (generation !== this.loadGeneration) {
          return;
        }

        this.loading.set(false);

        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.structured.set(null);
          return;
        }

        if (isRequestAborted(error)) {
          return;
        }

        this.error.set(this.readErrorMessage(error, 'Could not load structured CV content.'));
      }
    });
  }

  /**
   * Persist sections. Coalesces overlapping PUTs to the latest payload (never cancels an
   * in-flight HTTP PUT — unsubscribe would not cancel the server). Returns the save generation.
   */
  save(sections: readonly CvStructuredSection[], sectionId: string): number {
    const generation = ++this.saveGeneration;
    this.saveError.set(null);
    this.enqueueOrStartSave({
      sections,
      sectionId,
      generation
    });
    return generation;
  }

  /**
   * Ephemeral multi-section Update propose. Does not mutate Structured CV until Approve.
   */
  proposeUpdate(instructions: string, sectionIds?: readonly string[]): void {
    const trimmedInstructions = instructions.trim();

    if (!trimmedInstructions || this.proposingUpdate()) {
      return;
    }

    this.cancelUpdatePropose();
    this.proposingUpdate.set(true);
    this.aiUpdateError.set(null);

    this.updateProposeSubscription = this.apiService
      .proposeStructuredUpdate(trimmedInstructions, sectionIds)
      .subscribe({
        next: (proposal) => {
          this.proposingUpdate.set(false);
          this.updateProposal.set({
            ...proposal,
            focusSectionIds: proposal.focusSectionIds ?? [],
            changeBullets: proposal.changeBullets ?? [],
            proposedSections: this.hydrateProposedSections(proposal.proposedSections ?? [])
          });
          this.discardSummaryProposal();
        },
        error: (error) => {
          this.proposingUpdate.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          this.aiUpdateError.set(
            this.readErrorMessage(error, 'Could not propose structured CV updates with AI.')
          );
        }
      });
  }

  /** Clear in-memory Update proposal only (Discard). */
  discardUpdateProposal(): void {
    this.cancelUpdatePropose();
    this.updateProposal.set(null);
    this.aiUpdateError.set(null);
    this.proposingUpdate.set(false);
  }

  /**
   * Approve: merge proposed sections into local draft, persist via existing save,
   * then clear proposal. Does not call ai-update (ADR-0011).
   */
  approveUpdateProposal(localSections?: readonly CvStructuredSection[]): void {
    const proposal = this.updateProposal();
    const document = this.structured();

    if (!proposal || !document || proposal.proposedSections.length === 0) {
      return;
    }

    const baseSections = localSections ?? document.sections;
    const proposedDocument: CvStructuredDocument = {
      documentId: proposal.documentId || document.documentId,
      structuredImportedAt: document.structuredImportedAt,
      sections: proposal.proposedSections
    };
    const focusIds =
      proposal.focusSectionIds.length > 0 ? proposal.focusSectionIds : undefined;
    const merged = mergeAssistStructuredUpdate(
      { ...document, sections: cloneSectionsForDraft(baseSections) },
      proposedDocument,
      focusIds
    );

    this.setStructured(merged);
    const persistSectionId =
      focusIds?.[0] ?? merged.sections[0]?.id ?? 'assist-update-approve';
    this.save(merged.sections, persistSectionId);
    this.discardUpdateProposal();
  }

  generateSuggestions(sectionIds?: readonly string[], maxSuggestions = 6): void {
    if (this.generatingSuggestions()) {
      return;
    }

    this.cancelSuggestions();
    this.generatingSuggestions.set(true);
    this.suggestionError.set(null);

    this.suggestionsSubscription = this.apiService
      .generateStructuredSuggestions(sectionIds, maxSuggestions)
      .subscribe({
        next: (result) => {
          this.generatingSuggestions.set(false);
          this.suggestions.set(result.suggestions);
        },
        error: (error) => {
          this.generatingSuggestions.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          this.suggestionError.set(
            this.readErrorMessage(error, 'Could not generate CV improvement suggestions.')
          );
        }
      });
  }

  /**
   * Run ephemeral CV quality evaluation. Does not mutate Structured CV.
   * Result stays in memory only — never localStorage / IndexedDB / backend save (D2).
   */
  evaluateQuality(maxFindings = 8): void {
    if (this.evaluating()) {
      return;
    }

    this.cancelEvaluation();
    this.evaluating.set(true);
    this.evaluationError.set(null);

    this.evaluationSubscription = this.apiService.evaluateStructuredQuality(maxFindings).subscribe({
      next: (result) => {
        this.evaluating.set(false);
        this.evaluation.set(result);
      },
      error: (error) => {
        this.evaluating.set(false);

        if (isRequestAborted(error)) {
          return;
        }

        this.evaluationError.set(
          this.readErrorMessage(error, 'Could not evaluate CV quality.')
        );
      }
    });
  }

  clearSaveError(): void {
    this.saveError.set(null);
  }

  clearAiUpdateError(): void {
    this.aiUpdateError.set(null);
  }

  clearSuggestionError(): void {
    this.suggestionError.set(null);
  }

  clearSuggestions(): void {
    this.suggestions.set([]);
    this.suggestionError.set(null);
  }

  clearEvaluationError(): void {
    this.evaluationError.set(null);
  }

  clearEvaluation(): void {
    this.cancelEvaluation();
    this.evaluation.set(null);
    this.evaluationError.set(null);
    this.evaluating.set(false);
  }

  /**
   * Ephemeral Summary regeneration propose. Does not mutate Structured CV.
   * Optional instructions; empty/omit regenerates from CV context alone.
   */
  proposeSummary(instructions?: string): void {
    if (this.proposing()) {
      return;
    }

    this.cancelSummaryPropose();
    this.proposing.set(true);
    this.summaryProposalError.set(null);

    this.summaryProposeSubscription = this.apiService
      .proposeSummaryRegeneration(instructions)
      .subscribe({
        next: (proposal) => {
          this.proposing.set(false);
          this.summaryProposal.set(proposal);
          this.discardUpdateProposal();
        },
        error: (error) => {
          this.proposing.set(false);

          if (isRequestAborted(error)) {
            return;
          }

          this.summaryProposalError.set(
            this.readErrorMessage(error, 'Could not regenerate CV summary.')
          );
        }
      });
  }

  /** Clear in-memory Summary proposal only (D4 Discard). */
  discardSummaryProposal(): void {
    this.cancelSummaryPropose();
    this.summaryProposal.set(null);
    this.summaryProposalError.set(null);
    this.proposing.set(false);
  }

  /**
   * Approve: patch Summary section only in local sections, persist via existing save,
   * then clear proposal. Does not call ai-update (D4).
   * Pass `localSections` (draft ?? server) so unsaved edits to other sections survive.
   */
  approveSummaryProposal(localSections?: readonly CvStructuredSection[]): void {
    const proposal = this.summaryProposal();
    const document = this.structured();
    const proposedText = proposal?.proposedSummaryText?.trim() ?? '';

    if (!proposal || !document || !proposedText) {
      return;
    }

    const baseSections = localSections ?? document.sections;
    const { sections, summarySectionId } = this.patchSummaryOnly(baseSections, proposedText);

    this.setStructured({
      ...document,
      sections
    });
    this.save(sections, summarySectionId);
    this.discardSummaryProposal();
  }

  /**
   * Single Structured CV edit ingress: pass the raw API DTO (do not pre-hydrate).
   * Applies `hydrateForContentEditing` (hydrate + ADR-0003 edit normalize).
   */
  setStructured(document: CvStructuredDocument): void {
    this.structured.set(this.hydrateForContentEditing(document));
  }

  /**
   * Hydrate API payload and normalize edit slots (Summary/Skills/Contact),
   * including Contact modern expand + absorb/dedupe so Minimal/Modern canvases
   * never show unlabeled orphans beside empty Email/Phone/LinkedIn starters.
   * Sole normalize path for edit (ADR-0003); keep idempotent — see util specs.
   */
  hydrateForContentEditing(document: CvStructuredDocument): CvStructuredDocument {
    const hydrated = hydrateStructuredDocument(document);

    return {
      ...hydrated,
      // Full Contact modernize + absorb/dedupe (not only per-entry summary→bullets).
      sections: normalizeSectionsForEditing(hydrated.sections)
    };
  }

  private enqueueOrStartSave(request: PendingStructuredSave): void {
    if (this.saveSubscription) {
      // Keep only the latest intent; let the in-flight PUT finish, then send coalesced payload.
      this.pendingSave = request;
      this.savingSectionId.set(request.sectionId);
      return;
    }

    this.startSave(request);
  }

  private startSave(request: PendingStructuredSave): void {
    this.savingSectionId.set(request.sectionId);

    this.saveSubscription = this.apiService.saveStructured(toSaveRequest(request.sections)).subscribe({
      next: (document) => {
        this.saveSubscription = null;

        const pending = this.pendingSave;
        this.pendingSave = null;

        if (pending && pending.generation > request.generation) {
          // Stale response relative to a newer local intent — do not apply; send latest.
          this.startSave(pending);
          return;
        }

        if (request.generation !== this.saveGeneration) {
          this.savingSectionId.set(null);
          return;
        }

        this.savingSectionId.set(null);
        this.setStructured(document);
        this.lastSuccessfulSaveGeneration.set(request.generation);
      },
      error: (error) => {
        this.saveSubscription = null;

        const pending = this.pendingSave;
        this.pendingSave = null;

        if (pending && pending.generation > request.generation) {
          // Failed older PUT; still attempt the latest coalesced payload.
          this.startSave(pending);
          return;
        }

        this.savingSectionId.set(null);

        if (isRequestAborted(error)) {
          return;
        }

        if (request.generation !== this.saveGeneration) {
          return;
        }

        this.saveError.set(this.readErrorMessage(error, 'Could not save structured CV content.'));
      }
    });
  }

  private cancelLoadSubscription(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private cancelAiUpdate(): void {
    this.aiUpdateSubscription?.unsubscribe();
    this.aiUpdateSubscription = null;
  }

  private cancelUpdatePropose(): void {
    this.updateProposeSubscription?.unsubscribe();
    this.updateProposeSubscription = null;
  }

  private cancelSuggestions(): void {
    this.suggestionsSubscription?.unsubscribe();
    this.suggestionsSubscription = null;
  }

  private cancelEvaluation(): void {
    this.evaluationSubscription?.unsubscribe();
    this.evaluationSubscription = null;
  }

  private cancelSummaryPropose(): void {
    this.summaryProposeSubscription?.unsubscribe();
    this.summaryProposeSubscription = null;
  }

  private hydrateProposedSections(
    sections: readonly CvStructuredSection[]
  ): CvStructuredSection[] {
    return this.hydrateForContentEditing({
      documentId: 'proposal',
      structuredImportedAt: null,
      sections: cloneSectionsForDraft(sections)
    }).sections;
  }

  /**
   * Patch first Summary section (by sortOrder) entry `summary` field, or append a starter
   * Summary section when absent. Other sections unchanged.
   */
  private patchSummaryOnly(
    sections: readonly CvStructuredSection[],
    proposedSummaryText: string
  ): { sections: CvStructuredSection[]; summarySectionId: string } {
    const next = cloneSectionsForDraft(sections);
    const summarySections = next
      .filter((section) => section.sectionType === 'Summary')
      .sort((left, right) => left.sortOrder - right.sortOrder);
    const existing = summarySections[0];

    if (!existing) {
      const created = createSectionOfType('Summary', next.length);
      const entry = created.entries[0] ?? createEmptyEntry(0);
      created.entries = [{ ...entry, summary: proposedSummaryText }];
      next.push(created);
      return { sections: next, summarySectionId: created.id };
    }

    const index = next.findIndex((section) => section.id === existing.id);
    const section = next[index];
    const entriesByOrder = [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);
    const firstEntry = entriesByOrder[0];

    if (!firstEntry) {
      section.entries = [{ ...createEmptyEntry(0), summary: proposedSummaryText }];
    } else {
      section.entries = section.entries.map((entry) =>
        entry.id === firstEntry.id ? { ...entry, summary: proposedSummaryText } : entry
      );
    }

    return { sections: next, summarySectionId: section.id };
  }

  private readErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const payload = (error as { error: unknown }).error;

      if (typeof payload === 'string' && payload.trim()) {
        return payload;
      }
    }

    return fallback;
  }
}
