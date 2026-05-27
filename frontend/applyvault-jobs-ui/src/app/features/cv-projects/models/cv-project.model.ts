export interface GitHubRepositoryListItem {
  readonly externalRepoId: number;
  readonly fullName: string;
  readonly name: string;
  readonly description: string | null;
  readonly htmlUrl: string;
  readonly primaryLanguage: string | null;
  readonly topics: readonly string[];
  readonly isFork: boolean;
  readonly isArchived: boolean;
  readonly isPrivate: boolean;
  readonly starCount: number;
  readonly pushedAt: string | null;
}

export interface CvProjectSummary {
  readonly id: string;
  readonly externalRepoId: number;
  readonly fullName: string;
  readonly htmlUrl: string;
  readonly primaryLanguage: string | null;
  readonly topics: readonly string[];
  readonly cvTitle: string;
  readonly cvSummary: string;
  readonly cvBullets: readonly string[];
  readonly techStack: string;
  readonly generatedAt: string;
  readonly updatedAt: string;
  readonly includeInMerge: boolean;
  readonly mergeSectionHeading: string | null;
  readonly mergeSortOrder: number;
}

export interface CvPdfSection {
  readonly headingText: string;
  readonly normalizedKey: string;
  readonly pageIndex: number;
  readonly yPoints: number;
}

export interface CvProjectSummaryPlacement {
  readonly summaryId: string;
  readonly includeInMerge: boolean;
  readonly mergeSectionHeading: string | null;
  readonly mergeSortOrder: number;
}

export interface UpdateCvProjectSummaryPlacementsRequest {
  readonly placements: readonly CvProjectSummaryPlacement[];
}

export interface GitHubRepositoryReadme {
  readonly text: string | null;
}

export interface GenerateCvProjectRequest {
  readonly fullName: string;
}
