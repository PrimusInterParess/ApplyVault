import { Component, input, output } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { CvSectionType, CvStructuredEntry } from '../../models/cv-structured.model';
import { entryBodySourceText } from '../../utils/cv-structured-edit-normalizer.util';
import { CvMarkdownFieldComponent } from '../cv-markdown-field/cv-markdown-field.component';

@Component({
  selector: 'app-cv-structured-entry-editor',
  standalone: true,
  imports: [CvMarkdownFieldComponent],
  templateUrl: './cv-structured-entry-editor.component.html',
  styleUrl: './cv-structured-entry-editor.component.scss'
})
export class CvStructuredEntryEditorComponent {
  readonly entry = input.required<CvStructuredEntry>();
  readonly sectionType = input.required<CvSectionType>();
  readonly isContactSection = input(false);
  readonly disabled = input(false);
  readonly canMoveUp = input(false);
  readonly canMoveDown = input(false);
  readonly entryIndex = input(0);
  readonly fieldRevision = input(0);

  readonly entryChange = output<Partial<CvStructuredEntry>>();
  readonly moveUp = output<void>();
  readonly moveDown = output<void>();
  readonly remove = output<void>();

  protected isSummarySection(): boolean {
    return this.sectionType() === 'Summary';
  }

  protected isSkillsSection(): boolean {
    return this.sectionType() === 'Skills';
  }

  protected updateField(
    field: 'title' | 'subtitle' | 'dateRange' | 'summary' | 'techStack',
    event: Event
  ): void {
    this.entryChange.emit({ [field]: readInputValue(event) });
  }

  protected updateSummary(value: string): void {
    this.entryChange.emit({ summary: value });
  }

  protected updateBullet(index: number, value: string): void {
    const bullets = [...this.entry().bullets];
    bullets[index] = value;
    this.entryChange.emit({ bullets });
  }

  protected addBullet(): void {
    this.entryChange.emit({ bullets: [...this.entry().bullets, ''] });
  }

  protected removeBullet(index: number): void {
    const bullets = this.entry().bullets.filter((_, bulletIndex) => bulletIndex !== index);
    this.entryChange.emit({ bullets });
  }

  protected updateContactLine(index: number, event: Event): void {
    this.updateBullet(index, readInputValue(event));
  }

  protected addContactLine(): void {
    this.addBullet();
  }

  protected removeContactLine(index: number): void {
    this.removeBullet(index);
  }

  protected updateTechStack(event: Event): void {
    const value = readInputValue(event);
    const items = value
      .split(/[,;|]/)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    this.entryChange.emit({ techStack: value, bullets: items });
  }

  protected summaryFieldText(): string {
    return entryBodySourceText(this.entry(), this.sectionType());
  }

  protected skillsFieldText(): string {
    const techStack = this.entry().techStack.trim();

    if (techStack) {
      return this.entry().techStack;
    }

    const bullets = this.entry().bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0);

    if (bullets.length > 0) {
      return bullets.join(', ');
    }

    return this.entry().techStack;
  }
}
