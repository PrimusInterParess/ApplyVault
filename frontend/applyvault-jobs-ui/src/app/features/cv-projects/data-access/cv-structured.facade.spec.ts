import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';

import { CvStructuredDocument, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptySection } from '../utils/cv-structured-draft.util';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';

describe('CvStructuredFacade save generations / coalesce', () => {
  let facade: CvStructuredFacade;
  let saveSubjects: Subject<CvStructuredDocument>[];
  let getSubjects: Subject<CvStructuredDocument>[];

  function section(heading: string): CvStructuredSection {
    return {
      ...createEmptySection(0),
      heading,
      sectionType: 'Summary'
    };
  }

  function documentWith(heading: string): CvStructuredDocument {
    return {
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: [section(heading)]
    };
  }

  beforeEach(() => {
    saveSubjects = [];
    getSubjects = [];

    TestBed.configureTestingModule({
      providers: [
        CvStructuredFacade,
        {
          provide: CvDocumentApiService,
          useValue: {
            getStructured: () => {
              const subject = new Subject<CvStructuredDocument>();
              getSubjects.push(subject);
              return subject.asObservable();
            },
            saveStructured: () => {
              const subject = new Subject<CvStructuredDocument>();
              saveSubjects.push(subject);
              return subject.asObservable();
            },
            updateStructuredWithAi: () => new Subject<CvStructuredDocument>().asObservable(),
            generateStructuredSuggestions: () =>
              new Subject<{ suggestions: [] }>().asObservable()
          }
        }
      ]
    });

    facade = TestBed.inject(CvStructuredFacade);
  });

  it('ignores stale load responses after a newer load starts', () => {
    facade.load();
    facade.load();

    expect(getSubjects.length).toBe(2);

    getSubjects[0].next(documentWith('stale'));
    getSubjects[0].complete();

    expect(facade.structured()).toBeNull();

    getSubjects[1].next(documentWith('fresh'));
    getSubjects[1].complete();

    expect(facade.structured()?.sections[0].heading).toBe('fresh');
  });

  it('coalesces overlapping saves to the latest payload and ignores stale apply', () => {
    const gen1 = facade.save([section('first')], 'sec-a');
    const gen2 = facade.save([section('second')], 'sec-b');

    expect(gen1).toBe(1);
    expect(gen2).toBe(2);
    expect(saveSubjects.length).toBe(1);
    expect(facade.savingSectionId()).toBe('sec-b');

    saveSubjects[0].next(documentWith('first'));
    saveSubjects[0].complete();

    // Stale first response must not stick; coalesced second PUT starts.
    expect(facade.lastSuccessfulSaveGeneration()).toBe(0);
    expect(saveSubjects.length).toBe(2);
    expect(facade.savingSectionId()).toBe('sec-b');

    saveSubjects[1].next(documentWith('second'));
    saveSubjects[1].complete();

    expect(facade.lastSuccessfulSaveGeneration()).toBe(2);
    expect(facade.structured()?.sections[0].heading).toBe('second');
    expect(facade.isSaving()).toBeFalse();
  });

  it('does not start a second concurrent PUT while one is in flight', () => {
    facade.save([section('a')], 'a');
    facade.save([section('b')], 'b');
    facade.save([section('c')], 'c');

    expect(saveSubjects.length).toBe(1);

    saveSubjects[0].next(documentWith('a'));
    saveSubjects[0].complete();

    expect(saveSubjects.length).toBe(2);

    saveSubjects[1].next(documentWith('c'));
    saveSubjects[1].complete();

    expect(facade.lastSuccessfulSaveGeneration()).toBe(3);
    expect(facade.structured()?.sections[0].heading).toBe('c');
  });
});
