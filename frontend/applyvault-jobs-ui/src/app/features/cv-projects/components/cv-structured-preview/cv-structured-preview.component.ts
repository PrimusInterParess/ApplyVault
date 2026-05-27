import { Component, computed, input } from '@angular/core';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import {
  CvSectionType,
  CvStructuredEntry,
  CvStructuredSection
} from '../../models/cv-structured.model';

@Component({
  selector: 'app-cv-structured-preview',
  standalone: true,
  imports: [SkeletonBlockComponent],
  templateUrl: './cv-structured-preview.component.html',
  styleUrl: './cv-structured-preview.component.scss'
})
export class CvStructuredPreviewComponent {
  readonly sections = input<readonly CvStructuredSection[]>([]);
  readonly profilePhotoUrl = input<string | null>(null);
  readonly loadingPhoto = input(false);
  readonly loadingContent = input(false);

  readonly sortedSections = computed(() =>
    [...this.sections()]
      .map((section) => ({
        ...section,
        entries: [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder)
      }))
      .filter((section) => section.entries.length > 0)
      .sort((left, right) => left.sortOrder - right.sortOrder)
  );

  readonly hasContent = computed(() => this.sortedSections().length > 0);

  protected isSummarySection(sectionType: CvSectionType): boolean {
    return sectionType === 'Summary';
  }

  protected isSkillsSection(sectionType: CvSectionType): boolean {
    return sectionType === 'Skills';
  }

  protected hasEntryMeta(entry: CvStructuredEntry): boolean {
    return Boolean(entry.subtitle?.trim() || entry.dateRange?.trim());
  }

  protected hasTechStack(entry: CvStructuredEntry): boolean {
    return Boolean(entry.techStack?.trim());
  }

  protected techStackItems(entry: CvStructuredEntry): readonly string[] {
    const techStack = entry.techStack?.trim();

    if (!techStack) {
      return [];
    }

    return techStack
      .split(/[,;|]/)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }
}
