import { NgClass, NgTemplateOutlet } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { InlineEditableTextDirective } from '../../directives/inline-editable-text.directive';
import { CvSectionType, CvStructuredEntry, CvStructuredSection } from '../../models/cv-structured.model';
import {
  contactDisplayLine,
  contactEntryValue,
  contactFieldEntries,
  contactFieldsWithValues,
  contactSectionForDisplay,
  contactValuePlaceholder,
  findContactNameEntry,
  isContactNameEntry
} from '../../utils/cv-contact-channels.util';
import {
  createSamplePreviewSections,
  partitionSectionsForTemplate
} from '../../utils/cv-export-template-layout.util';
import {
  CvTemplateInlineEdit,
  addEntryLabelForSection,
  addableSectionTypes,
  resolveContactDisplayName,
  sectionAllowsMultipleEntries
} from '../../utils/cv-template-inline-edit.util';

@Component({
  selector: 'app-cv-export-template-preview',
  standalone: true,
  imports: [NgClass, NgTemplateOutlet, InlineEditableTextDirective],
  templateUrl: './cv-export-template-preview.component.html',
  styleUrl: './cv-export-template-preview.component.scss',
  host: {
    '[class.cv-export-preview-host--editable]': 'editable()'
  }
})
export class CvExportTemplatePreviewComponent {
  readonly templateId = input.required<number>();
  readonly sections = input<readonly CvStructuredSection[]>([]);
  readonly sampleMode = input(false);
  readonly compact = input(false);
  readonly editable = input(false);
  readonly profilePhotoUrl = input<string | null>(null);

  readonly inlineEdit = output<CvTemplateInlineEdit>();

  protected readonly displayName = computed(() => {
    if (this.sampleMode()) {
      return 'Alex Jensen';
    }

    const contact = this.effectiveSections().find((section) => this.isContactSection(section));
    return resolveContactDisplayName(contact);
  });

  protected readonly contactNameEntry = computed(() => {
    const contact = this.effectiveSections().find((section) => this.isContactSection(section));
    return contact ? findContactNameEntry(contact) : null;
  });

  protected readonly layout = computed(() =>
    partitionSectionsForTemplate(this.templateId(), this.effectiveSections(), {
      includeEmpty: this.editable() || !this.sampleMode()
    })
  );

  protected readonly templateClass = computed(
    () => `cv-export-preview--template-${this.templateId()}`
  );

  protected readonly addableTypes = computed(() => addableSectionTypes(this.effectiveSections()));

  private readonly effectiveSections = computed(() => {
    if (this.sampleMode()) {
      return createSamplePreviewSections();
    }

    return this.sections();
  });

  protected isSummarySection(sectionType: string): boolean {
    return sectionType === 'Summary';
  }

  protected isSkillsSection(sectionType: string): boolean {
    return sectionType === 'Skills';
  }

  protected isContactSection(section: CvStructuredSection): boolean {
    return section.sectionType === 'Contact' || section.heading.trim().toLowerCase() === 'contact';
  }

  protected allowsMultipleEntries(section: CvStructuredSection): boolean {
    return sectionAllowsMultipleEntries(section.sectionType);
  }

  protected addEntryLabel(section: CvStructuredSection): string {
    return addEntryLabelForSection(section.sectionType);
  }

  protected canRemoveEntry(section: CvStructuredSection, entry: CvStructuredEntry): boolean {
    if (this.isContactSection(section)) {
      return !isContactNameEntry(entry);
    }

    return this.allowsMultipleEntries(section) && section.entries.length > 1;
  }

  protected showContactNameInSection(section: CvStructuredSection): boolean {
    // Classic (1) and Minimal (3) already edit the name in the page header.
    const templateId = Number(this.templateId());
    return this.isContactSection(section) && templateId !== 1 && templateId !== 3;
  }

  /**
   * Match BE AppendClassicContactHeader for all templates: Contact has no section title
   * (Classic/Minimal header + Modern sidebar all emit name + value lines only).
   */
  protected showSectionTitle(section: CvStructuredSection): boolean {
    return !this.isContactSection(section);
  }

  /** Contact channels only (BE cv-contact-line parity). Name handled separately. */
  protected visibleContactChannels(section: CvStructuredSection): readonly CvStructuredEntry[] {
    if (!this.isContactSection(section)) {
      return [];
    }

    const view = contactSectionForDisplay(section);
    return this.editable() ? contactFieldEntries(view) : contactFieldsWithValues(view);
  }

  /** Modern sidebar: Name lives in the Contact block (Classic/Minimal use page header). */
  protected visibleContactName(section: CvStructuredSection): CvStructuredEntry | null {
    if (!this.showContactNameInSection(section)) {
      return null;
    }

    const view = contactSectionForDisplay(section);
    const name = findContactNameEntry(view);

    if (!name) {
      return null;
    }

    if (!this.editable() && !(name.subtitle?.trim())) {
      return null;
    }

    return name;
  }

  protected visibleContactEntries(section: CvStructuredSection): readonly CvStructuredEntry[] {
    if (!this.isContactSection(section)) {
      return this.sortedEntries(section);
    }

    const name = this.visibleContactName(section);
    const fields = this.visibleContactChannels(section);
    return name ? [name, ...fields] : fields;
  }

  protected contactValue(entry: CvStructuredEntry): string {
    return contactEntryValue(entry);
  }

  protected contactPlaceholder(entry: CvStructuredEntry): string {
    return contactValuePlaceholder(entry);
  }

  protected isNameEntry(entry: CvStructuredEntry): boolean {
    return isContactNameEntry(entry);
  }

  protected contactNameText(entry: CvStructuredEntry): string {
    return entry.subtitle?.trim() || '';
  }

  protected skillItems(entry: CvStructuredEntry): readonly string[] {
    const techStack = entry.techStack?.trim();

    if (techStack) {
      return techStack
        .split(/[,;|]/)
        .map((item) => item.trim())
        .filter((item) => item.length > 0);
    }

    return entry.bullets.map((bullet) => bullet.trim()).filter((bullet) => bullet.length > 0);
  }

  protected skillsLine(entry: CvStructuredEntry): string {
    return this.skillItems(entry).join(', ');
  }

  protected contactLines(section: CvStructuredSection): readonly string[] {
    const view = contactSectionForDisplay(section);
    const fields = contactFieldsWithValues(view)
      .map((entry) => contactDisplayLine(entry))
      .filter((line) => line.length > 0);

    if (fields.length > 0) {
      return fields;
    }

    return section.entries.flatMap((entry) =>
      entry.bullets.map((line) => line.trim()).filter((line) => line.length > 0)
    );
  }

  protected bulletSlots(section: CvStructuredSection, entry: CvStructuredEntry): readonly { index: number; text: string }[] {
    const count = Math.max(2, entry.bullets.length);

    return Array.from({ length: count }, (_, index) => ({
      index,
      text: entry.bullets[index]?.trim() ?? ''
    }));
  }

  protected sortedEntries(section: CvStructuredSection): readonly CvStructuredEntry[] {
    return [...section.entries].sort((left, right) => left.sortOrder - right.sortOrder);
  }

  protected emitContactName(entry: CvStructuredEntry, value: string): void {
    const section = this.effectiveSections().find((item) => this.isContactSection(item));

    if (!section) {
      return;
    }

    this.emitEntryField(section.id, entry.id, 'subtitle', value);
  }

  protected emitSectionHeading(sectionId: string, value: string): void {
    this.inlineEdit.emit({ kind: 'sectionHeading', sectionId, value });
  }

  protected emitEntryField(
    sectionId: string,
    entryId: string,
    field: 'title' | 'subtitle' | 'dateRange' | 'summary' | 'techStack',
    value: string
  ): void {
    this.inlineEdit.emit({ kind: 'entryField', sectionId, entryId, field, value });
  }

  protected emitBullet(sectionId: string, entryId: string, index: number, value: string): void {
    this.inlineEdit.emit({ kind: 'bullet', sectionId, entryId, index, value });
  }

  protected emitSkillsLine(sectionId: string, entryId: string, value: string): void {
    this.inlineEdit.emit({ kind: 'skillsLine', sectionId, entryId, value });
  }

  protected emitAddEntry(sectionId: string): void {
    this.inlineEdit.emit({ kind: 'addEntry', sectionId });
  }

  protected emitRemoveEntry(sectionId: string, entryId: string): void {
    this.inlineEdit.emit({ kind: 'removeEntry', sectionId, entryId });
  }

  protected sectionIndex(sectionId: string): number {
    return this.effectiveSections().findIndex((section) => section.id === sectionId);
  }

  protected canMoveSectionUp(sectionId: string): boolean {
    return this.sectionIndex(sectionId) > 0;
  }

  protected canMoveSectionDown(sectionId: string): boolean {
    const index = this.sectionIndex(sectionId);
    return index >= 0 && index < this.effectiveSections().length - 1;
  }

  protected emitMoveSection(sectionId: string, direction: -1 | 1): void {
    const fromIndex = this.sectionIndex(sectionId);
    const toIndex = fromIndex + direction;

    if (fromIndex < 0 || toIndex < 0 || toIndex >= this.effectiveSections().length) {
      return;
    }

    this.inlineEdit.emit({ kind: 'reorderSections', fromIndex, toIndex });
  }

  protected emitRemoveSection(sectionId: string): void {
    this.inlineEdit.emit({ kind: 'removeSection', sectionId });
  }

  protected emitAddSection(sectionType: string): void {
    if (!this.addableTypes().includes(sectionType as CvSectionType)) {
      return;
    }

    this.inlineEdit.emit({ kind: 'addSection', sectionType: sectionType as CvSectionType });
  }
}
