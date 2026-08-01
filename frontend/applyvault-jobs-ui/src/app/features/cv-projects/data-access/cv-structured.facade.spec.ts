import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';

import { CvStructuredDocument, CvStructuredSection } from '../models/cv-structured.model';
import { createEmptyEntry, createEmptySection } from '../utils/cv-structured-draft.util';
import { CvDocumentApiService } from './cv-document-api.service';
import { CvStructuredFacade } from './cv-structured.facade';

describe('CvStructuredFacade save generations / coalesce', () => {
  let facade: CvStructuredFacade;
  let saveSubjects: Subject<CvStructuredDocument>[];
  let getSubjects: Subject<CvStructuredDocument>[];
  let aiUpdateSubjects: Subject<CvStructuredDocument>[];

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
    aiUpdateSubjects = [];

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
            updateStructuredWithAi: () => {
              const subject = new Subject<CvStructuredDocument>();
              aiUpdateSubjects.push(subject);
              return subject.asObservable();
            },
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

  it('normalizes Summary title→summary and Skills bullets→techStack on load', () => {
    facade.load();

    getSubjects[0].next({
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: [
        {
          ...createEmptySection(0),
          id: 'summary-1',
          heading: 'Summary',
          sectionType: 'Summary',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 's1',
              title: 'Imported profile text',
              summary: ''
            }
          ]
        },
        {
          ...createEmptySection(1),
          id: 'skills-1',
          heading: 'Skills',
          sectionType: 'Skills',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'k1',
              bullets: ['TypeScript', 'Angular'],
              techStack: ''
            }
          ]
        }
      ]
    });
    getSubjects[0].complete();

    const summary = facade.structured()?.sections.find((item) => item.sectionType === 'Summary');
    const skills = facade.structured()?.sections.find((item) => item.sectionType === 'Skills');

    expect(summary?.entries[0].summary).toBe('Imported profile text');
    expect(skills?.entries[0].techStack).toBe('TypeScript, Angular');
    expect(skills?.entries[0].bullets).toEqual([]);
  });

  it('setStructured hydrates+normalizes without requiring a pre-hydrate from callers', () => {
    facade.setStructured({
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: [
        {
          ...createEmptySection(0),
          id: 'skills-1',
          heading: 'Skills',
          sectionType: 'Skills',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'k1',
              bullets: ['RxJS'],
              techStack: ''
            }
          ]
        }
      ]
    });

    expect(facade.structured()?.sections[0].entries[0].techStack).toBe('RxJS');
    expect(facade.structured()?.sections[0].entries[0].bullets).toEqual([]);
  });

  it('updateWithAi merges partial AI payload so non-selected sections survive and re-saves', () => {
    facade.setStructured({
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: [
        {
          ...createEmptySection(0),
          id: 'contact-1',
          heading: 'Contact',
          sectionType: 'Contact',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'name-1',
              title: 'Name',
              subtitle: 'Ada Lovelace'
            }
          ]
        },
        {
          ...createEmptySection(1),
          id: 'summary-1',
          heading: 'Summary',
          sectionType: 'Summary',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'sum-1',
              summary: 'Old summary'
            }
          ]
        },
        {
          ...createEmptySection(2),
          id: 'exp-1',
          heading: 'Experience',
          sectionType: 'Experience',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'exp-e1',
              title: 'Engineer',
              subtitle: 'Acme'
            }
          ]
        },
        {
          ...createEmptySection(3),
          id: 'edu-1',
          heading: 'Education',
          sectionType: 'Education',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'edu-e1',
              title: 'BSc',
              subtitle: 'Uni'
            }
          ]
        }
      ]
    });

    facade.updateWithAi('Rewrite summary for backend roles.', ['summary-1']);

    expect(aiUpdateSubjects.length).toBe(1);

    aiUpdateSubjects[0].next({
      documentId: 'doc-1',
      structuredImportedAt: null,
      sections: [
        {
          ...createEmptySection(0),
          id: 'contact-1',
          heading: 'Contact',
          sectionType: 'Contact',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'name-1',
              title: 'Name',
              subtitle: ''
            }
          ]
        },
        {
          ...createEmptySection(1),
          id: 'summary-1',
          heading: 'Summary',
          sectionType: 'Summary',
          entries: [
            {
              ...createEmptyEntry(0),
              id: 'sum-1',
              summary: 'Backend-focused summary for smoke test M7.'
            }
          ]
        }
      ]
    });
    aiUpdateSubjects[0].complete();

    const ids = facade.structured()?.sections.map((item) => item.id);
    expect(ids).toEqual(['contact-1', 'summary-1', 'exp-1', 'edu-1']);
    expect(facade.structured()?.sections.find((item) => item.id === 'summary-1')?.entries[0].summary).toBe(
      'Backend-focused summary for smoke test M7.'
    );
    expect(
      facade.structured()?.sections.find((item) => item.id === 'contact-1')?.entries.find(
        (entry) => entry.title === 'Name'
      )?.subtitle
    ).toBe('Ada Lovelace');
    expect(facade.structured()?.sections.find((item) => item.id === 'exp-1')?.entries[0].title).toBe(
      'Engineer'
    );
    expect(facade.structured()?.sections.find((item) => item.id === 'edu-1')?.entries[0].title).toBe(
      'BSc'
    );

    // Server already saved the partial AI body — corrective PUT restores preserved sections.
    expect(saveSubjects.length).toBe(1);
    expect(facade.savingSectionId()).toBe('summary-1');
  });
});
