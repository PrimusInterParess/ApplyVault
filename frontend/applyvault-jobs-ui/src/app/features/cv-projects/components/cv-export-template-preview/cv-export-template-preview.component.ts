import { NgClass, NgTemplateOutlet } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { InlineEditableTextDirective } from '../../directives/inline-editable-text.directive';
import { normalizeCvExportTemplateId } from '../../models/cv-export-template.model';
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
import { partitionSectionsForTemplate } from '../../utils/cv-export-template-layout.util';
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
  readonly compact = input(false);
  readonly editable = input(false);
  readonly profilePhotoUrl = input<string | null>(null);

  readonly inlineEdit = output<CvTemplateInlineEdit>();

  protected readonly displayName = computed(() => {
    const contact = this.sections().find((section) => this.isContactSection(section));
    return resolveContactDisplayName(contact);
  });

  protected readonly contactNameEntry = computed(() => {
    const contact = this.sections().find((section) => this.isContactSection(section));
    return contact ? findContactNameEntry(contact) : null;
  });

  /** Coerce so stringy inputs never miss Minimal (`=== 3`) vs Modern (`=== 2`). */
  protected readonly resolvedTemplateId = computed(() =>
    normalizeCvExportTemplateId(Number(this.templateId()))
  );

  protected readonly layout = computed(() =>
    partitionSectionsForTemplate(this.resolvedTemplateId(), this.sections(), {
      // Always include empty shells so the editable desk (and non-sample preview) stay stable.
      includeEmpty: true
    })
  );

  protected readonly templateClass = computed(
    () => `cv-export-preview--template-${this.resolvedTemplateId()}`
  );

  protected readonly addableTypes = computed(() => addableSectionTypes(this.sections()));

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

  /**
   * Import often yields Contact + Profile both typed Contact — only the first
   * owns name edit / add-channel chrome (BE contact-header flush parity).
   */
  protected isPrimaryContactSection(section: CvStructuredSection): boolean {
    if (!this.isContactSection(section)) {
      return false;
    }

    const firstContact = this.sections().find((item) => this.isContactSection(item));
    return firstContact?.id === section.id;
  }

  protected showContactNameInSection(section: CvStructuredSection): boolean {
    // Minimal (3) already edits the name in the page header.
    if (!this.isContactSection(section) || this.resolvedTemplateId() === 3) {
      return false;
    }

    return this.isPrimaryContactSection(section);
  }

  /**
   * Match BE contact header emission: Contact has no section title
   * (Minimal header + Modern sidebar emit name + value lines only).
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

  /**
   * Skip empty secondary Contact sections (e.g. Profile that only repeats Name).
   * Empty primary stays only when no sibling Contact already owns channels — otherwise
   * Minimal shows a lone "+" under the header name (import Contact + Profile shape).
   */
  protected showContactBlock(section: CvStructuredSection): boolean {
    if (!this.isContactSection(section)) {
      return false;
    }

    if (this.visibleContactChannels(section).length > 0) {
      return true;
    }

    if (!this.editable() || !this.isPrimaryContactSection(section)) {
      return false;
    }

    return !this.sections().some(
      (item) =>
        item.id !== section.id &&
        this.isContactSection(item) &&
        this.visibleContactChannels(item).length > 0
    );
  }

  /** One add control across Contact-typed sections (primary, else first with channels). */
  protected showContactAddButton(section: CvStructuredSection): boolean {
    if (!this.editable() || !this.isContactSection(section) || !this.showContactBlock(section)) {
      return false;
    }

    if (this.isPrimaryContactSection(section)) {
      return true;
    }

    const primary = this.sections().find((item) => this.isContactSection(item));

    if (primary && this.showContactBlock(primary)) {
      return false;
    }

    const firstWithChannels = this.sections().find(
      (item) => this.isContactSection(item) && this.visibleContactChannels(item).length > 0
    );

    return firstWithChannels?.id === section.id;
  }

  /** Drop Contact shells that have nothing to render (avoids a lone "+" under Minimal). */
  protected showContactSection(section: CvStructuredSection): boolean {
    if (!this.isContactSection(section)) {
      return true;
    }

    return this.showContactNameInSection(section) || this.showContactBlock(section);
  }

  /** Modern sidebar: Name lives in the Contact block (Minimal uses page header). */
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
    const section = this.sections().find((item) => this.isContactSection(item));

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
    return this.sections().findIndex((section) => section.id === sectionId);
  }

  protected canMoveSectionUp(sectionId: string): boolean {
    return this.sectionIndex(sectionId) > 0;
  }

  protected canMoveSectionDown(sectionId: string): boolean {
    const index = this.sectionIndex(sectionId);
    return index >= 0 && index < this.sections().length - 1;
  }

  protected emitMoveSection(sectionId: string, direction: -1 | 1): void {
    const fromIndex = this.sectionIndex(sectionId);
    const toIndex = fromIndex + direction;

    if (fromIndex < 0 || toIndex < 0 || toIndex >= this.sections().length) {
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
