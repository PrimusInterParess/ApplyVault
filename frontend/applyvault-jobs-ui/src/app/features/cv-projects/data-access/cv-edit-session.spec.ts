import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { signal } from '@angular/core';

import { CvStructuredSection } from '../models/cv-structured.model';
import { CvEditSession } from './cv-edit-session';
import { CvStructuredFacade } from './cv-structured.facade';

describe('CvEditSession draft lifecycle (characterization)', () => {
  let session: CvEditSession;
  let isSaving: ReturnType<typeof signal<boolean>>;
  let saveError: ReturnType<typeof signal<string | null>>;
  let lastSuccessfulSaveGeneration: ReturnType<typeof signal<number>>;
  let structured: ReturnType<typeof signal<{ documentId: string; structuredImportedAt: null; sections: CvStructuredSection[] } | null>>;
  let saveSpy: jasmine.Spy;
  let nextSaveGeneration: number;

  const baseSections: CvStructuredSection[] = [
    {
      id: 'sec-1',
      heading: 'Summary',
      sectionType: 'Summary',
      sortOrder: 0,
      entries: [
        {
          id: 'entry-1',
          title: 'Profile',
          subtitle: null,
          dateRange: null,
          summary: 'Hello',
          bullets: [],
          techStack: '',
          fields: {},
          source: 'Manual',
          sourceSummaryId: null,
          sortOrder: 0
        }
      ]
    }
  ];

  beforeEach(() => {
    nextSaveGeneration = 0;
    isSaving = signal(false);
    saveError = signal<string | null>(null);
    lastSuccessfulSaveGeneration = signal(0);
    structured = signal({
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: structuredClone(baseSections)
    });
    saveSpy = jasmine.createSpy('save').and.callFake(() => ++nextSaveGeneration);

    TestBed.configureTestingModule({
      providers: [
        CvEditSession,
        {
          provide: CvStructuredFacade,
          useValue: {
            structured: structured.asReadonly(),
            isSaving: isSaving.asReadonly(),
            saveError: saveError.asReadonly(),
            lastSuccessfulSaveGeneration: lastSuccessfulSaveGeneration.asReadonly(),
            save: saveSpy
          }
        }
      ]
    });

    session = TestBed.inject(CvEditSession);
  });

  afterEach(() => {
    session.cancelPendingSave();
  });

  it('clears draft when matching successful save generation completes and no newer local edit exists', fakeAsync(() => {
    session.apply({
      kind: 'entryField',
      sectionId: 'sec-1',
      entryId: 'entry-1',
      field: 'summary',
      value: 'Edited once'
    });

    expect(session.inlineDraft()).not.toBeNull();
    expect(session.saveStatus()).toBe('Unsaved changes');

    tick(500);
    expect(saveSpy).toHaveBeenCalledTimes(1);
    const savedGeneration = saveSpy.calls.mostRecent().returnValue as number;

    isSaving.set(true);
    // Allow effect to observe saving=true
    TestBed.flushEffects();

    lastSuccessfulSaveGeneration.set(savedGeneration);
    isSaving.set(false);
    TestBed.flushEffects();

    expect(session.inlineDraft()).toBeNull();
    expect(session.saveStatus()).toBe('Saved');
    expect(session.sections()[0].entries[0].summary).toBe('Hello');
  }));

  it('does not clear draft when a newer local edit exists after the save was requested', fakeAsync(() => {
    session.apply({
      kind: 'entryField',
      sectionId: 'sec-1',
      entryId: 'entry-1',
      field: 'summary',
      value: 'First edit'
    });

    tick(500);
    expect(saveSpy).toHaveBeenCalledTimes(1);
    const firstGeneration = saveSpy.calls.mostRecent().returnValue as number;

    // Newer local edit before first save success is observed.
    session.apply({
      kind: 'entryField',
      sectionId: 'sec-1',
      entryId: 'entry-1',
      field: 'summary',
      value: 'Second edit (sticky)'
    });

    expect(session.inlineDraft()?.[0].entries[0].summary).toBe('Second edit (sticky)');

    isSaving.set(true);
    TestBed.flushEffects();
    lastSuccessfulSaveGeneration.set(firstGeneration);
    isSaving.set(false);
    TestBed.flushEffects();

    expect(session.inlineDraft()).not.toBeNull();
    expect(session.inlineDraft()?.[0].entries[0].summary).toBe('Second edit (sticky)');
    expect(session.saveStatus()).toBe('Unsaved changes');
  }));

  it('coalesces rapid edits into one debounced save of the latest draft', fakeAsync(() => {
    session.apply({
      kind: 'entryField',
      sectionId: 'sec-1',
      entryId: 'entry-1',
      field: 'summary',
      value: 'A'
    });
    session.apply({
      kind: 'entryField',
      sectionId: 'sec-1',
      entryId: 'entry-1',
      field: 'summary',
      value: 'B'
    });

    expect(saveSpy).not.toHaveBeenCalled();
    tick(500);
    expect(saveSpy).toHaveBeenCalledTimes(1);
    const savedSections = saveSpy.calls.mostRecent().args[0] as CvStructuredSection[];
    expect(savedSections[0].entries[0].summary).toBe('B');
  }));
});
