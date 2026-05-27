import { EuresJobDetail, EuresJobListing, EuresJobSearchResponse } from '../app/features/eures-jobs/models/eures-job.model';
import { SavedJobResult } from '../app/features/job-results/models/job-result.model';

export const TEST_API_BASE_URL = 'http://localhost:5173/api';

export function createSavedJobResult(overrides: Partial<SavedJobResult> = {}): SavedJobResult {
  return {
    id: 'job-1',
    savedAt: '2026-05-01T12:00:00.000Z',
    isRejected: false,
    interviewDate: null,
    interviewEvent: null,
    calendarEvents: [],
    statusSync: null,
    payload: {
      title: 'Software Engineer',
      url: 'https://example.com/jobs/1',
      text: 'Job description text',
      textLength: 20,
      extractedAt: '2026-05-01T11:00:00.000Z',
      jobDetails: {
        sourceHostname: 'example.com',
        detectedPageType: 'job_posting',
        jobTitle: 'Software Engineer',
        companyName: 'Acme Corp',
        location: 'Copenhagen',
        jobDescription: 'Build things',
        positionSummary: null,
        hiringManagerName: null,
        hiringManagerContacts: []
      }
    },
    ...overrides
  };
}

export function createEuresListing(overrides: Partial<EuresJobListing> = {}): EuresJobListing {
  return {
    id: 'eures-1',
    title: 'Backend Developer',
    employer: 'Nordic Tech',
    location: 'Copenhagen',
    publicationDate: '2026-04-15T00:00:00.000Z',
    sourceUrl: 'https://europa.eu/eures/job/eures-1',
    ...overrides
  };
}

export function createEuresSearchResponse(
  overrides: Partial<EuresJobSearchResponse> = {}
): EuresJobSearchResponse {
  return {
    totalResults: 1,
    page: 1,
    resultsPerPage: 5,
    jobs: [createEuresListing()],
    ...overrides
  };
}

export function createEuresJobDetail(overrides: Partial<EuresJobDetail> = {}): EuresJobDetail {
  return {
    id: 'eures-1',
    title: 'Backend Developer',
    employer: 'Nordic Tech',
    location: 'Copenhagen',
    publicationDate: '2026-04-15T00:00:00.000Z',
    sourceUrl: 'https://europa.eu/eures/job/eures-1',
    description: 'Build APIs for public sector clients.',
    applicationUrl: 'https://europa.eu/eures/job/eures-1/apply',
    contractType: 'Permanent',
    workHours: 'Full time',
    ...overrides
  };
}
