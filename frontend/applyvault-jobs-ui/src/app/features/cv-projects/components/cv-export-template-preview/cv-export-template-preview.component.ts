import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, ElementRef, inject, input, output } from '@angular/core';

import { CvInlineLinkPopoverComponent } from '../cv-inline-link-popover/cv-inline-link-popover.component';
import { InlineEditableTextDirective } from '../../directives/inline-editable-text.directive';
import { CvStructuredEntry, CvStructuredSection } from '../../models/cv-structured.model';
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
  partitionSectionsForExport,
  sectionHasRenderableContent
} from '../../utils/cv-export-template-layout.util';
import {
  CvTemplateInlineEdit,
  addEntryLabelForSection,
  resolveContactDisplayName,
  sectionAllowsMultipleEntries
} from '../../utils/cv-template-inline-edit.util';
import {
  CV_A4_HEIGHT_PX,
  CV_CANVAS_FIT_SCALES,
  CV_PAGE_GAP_PX,
  cvPageBreakMargin
} from '../../utils/cv-canvas-pages.util';

@Component({
  selector: 'app-cv-export-template-preview',
  standalone: true,
  imports: [NgTemplateOutlet, InlineEditableTextDirective, CvInlineLinkPopoverComponent],
  templateUrl: './cv-export-template-preview.component.html',
  styleUrl: './cv-export-template-preview.component.scss',
  host: {
    '[class.cv-export-preview-host--editable]': 'editable()'
  }
})
export class CvExportTemplatePreviewComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly sections = input<readonly CvStructuredSection[]>([]);
  readonly compact = input(false);
  readonly editable = input(false);
  readonly profilePhotoUrl = input<string | null>(null);

  readonly inlineEdit = output<CvTemplateInlineEdit>();
  readonly photoClick = output<void>();

  constructor() {
    effect((onCleanup) => {
      if (!this.editable() || this.compact()) {
        return;
      }

      this.sections();

      if (typeof ResizeObserver === 'undefined') {
        return;
      }

      const observer = new ResizeObserver(() => this.paginateSheets());
      observer.observe(this.host.nativeElement);
      queueMicrotask(() => this.paginateSheets());

      onCleanup(() => observer.disconnect());
    });
  }

  protected readonly displayName = computed(() => {
    const contact = this.sections().find((section) => this.isContactSection(section));
    return resolveContactDisplayName(contact);
  });

  protected readonly contactNameEntry = computed(() => {
    const contact = this.sections().find((section) => this.isContactSection(section));
    return contact ? findContactNameEntry(contact) : null;
  });

  protected readonly layout = computed(() =>
    partitionSectionsForExport(this.sections(), {
      // Empty Sections stay on the canvas so they can be filled. Omitted Sections do not.
      includeEmpty: true,
      editCanvas: true
    })
  );

  protected readonly contactAnchorId = computed(() => {
    const contact = this.sections().find((section) => this.isContactSection(section));
    return contact ? this.sectionAnchorId(contact.id) : null;
  });

  protected sectionAnchorId(sectionId: string): string {
    return `cv-section-${sectionId}`;
  }

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

  protected sectionIsEmpty(section: CvStructuredSection): boolean {
    return !sectionHasRenderableContent(section);
  }

  protected showAddEntry(section: CvStructuredSection): boolean {
    if (!this.editable() || this.isContactSection(section)) {
      return false;
    }

    return this.allowsMultipleEntries(section) || section.entries.length === 0;
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

  /**
   * Match BE contact header emission: Contact has no section title
   * (header emits name + value lines only).
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
   * the canvas shows a lone "+" under the header name (import Contact + Profile shape).
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

  /**
   * Drop Contact shells that have nothing to render (avoids a lone "+" under the header).
   * The name is never rendered inside the Contact block — it is edited in the page header.
   */
  protected showContactSection(section: CvStructuredSection): boolean {
    if (!this.isContactSection(section)) {
      return true;
    }

    return this.showContactBlock(section);
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

  /**
   * A Section continues onto the next sheet between Entries. An Entry stays whole.
   * Spacing stays at the normal rhythm. Editing nodes stay mounted; only margin moves them.
   * A block that continues starts at the same top inset as page 1.
   */
  private paginateSheets(): void {
    if (!this.editable()) {
      return;
    }

    const page = this.host.nativeElement.querySelector('.cv-export-preview__page');

    if (!(page instanceof HTMLElement)) {
      return;
    }

    this.clearPageBreaks(page);
    const fitted = this.applyFitScale(page);

    if (fitted) {
      return;
    }

    const padTop = this.sheetPadTopPx(page);
    const header = page.querySelector('.cv-export-preview__minimal-header');

    if (header instanceof HTMLElement && header.offsetHeight <= CV_A4_HEIGHT_PX) {
      this.placeOnSheet(header, padTop);
    }

    page.querySelectorAll('.cv-export-preview__main--single > .cv-export-preview__section').forEach((node) => {
      if (!(node instanceof HTMLElement)) {
        return;
      }

      const entries = [...node.querySelectorAll(':scope > .cv-export-preview__entries > .cv-export-preview__entry')].filter(
        (entry): entry is HTMLElement => entry instanceof HTMLElement
      );

      this.placeSection(node, entries, padTop);
    });
  }

  private placeSection(section: HTMLElement, entries: readonly HTMLElement[], padTop: number): void {
    this.clearSheetMargin(section);
    entries.forEach((entry) => this.clearEntryMargins(entry));

    if (entries.length === 0) {
      this.placeOnSheet(section, padTop);
      return;
    }

    const leadHeight = this.leadSpan(section, [entries[0]]);
    const leadFitsPage = leadHeight > 0 && leadHeight <= CV_A4_HEIGHT_PX;
    const movedLead = leadFitsPage && this.pushTogether(section, leadHeight, padTop);
    const rest = movedLead ? entries.slice(1) : entries;
    rest.forEach((entry) => this.placeEntry(entry, padTop));
  }

  /** Keep an Entry that fits on one page whole. A taller Entry continues between bullets. */
  private placeEntry(entry: HTMLElement, padTop: number): void {
    this.clearEntryMargins(entry);
    const parts = this.entryParts(entry);

    if (
      entry.offsetHeight <= CV_A4_HEIGHT_PX ||
      (parts.bullets.length === 0 && parts.trailing.length === 0)
    ) {
      this.placeOnSheet(entry, padTop);
      return;
    }

    const leadHeight = this.leadSpan(entry, parts.lead);
    if (leadHeight > 0) {
      this.pushTogether(entry, leadHeight, padTop);
    }

    parts.bullets.forEach((bullet) => this.placeOnSheet(bullet, padTop));
    parts.trailing.forEach((line) => this.placeOnSheet(line, padTop));
  }

  private entryParts(entry: HTMLElement): {
    lead: HTMLElement[];
    bullets: HTMLElement[];
    trailing: HTMLElement[];
  } {
    const lead: HTMLElement[] = [];
    const bullets: HTMLElement[] = [];
    const trailing: HTMLElement[] = [];
    let seenBullets = false;

    entry.querySelectorAll(':scope > *').forEach((node) => {
      if (!(node instanceof HTMLElement)) {
        return;
      }

      if (node.classList.contains('cv-export-preview__entry-bullets')) {
        seenBullets = true;
        node.querySelectorAll(':scope > li').forEach((item) => {
          if (item instanceof HTMLElement) {
            bullets.push(item);
          }
        });
        return;
      }

      if (seenBullets) {
        trailing.push(node);
      } else {
        lead.push(node);
      }
    });

    return { lead, bullets, trailing };
  }

  /** Height from the origin's top through the last visible block. */
  private leadSpan(origin: HTMLElement, elements: readonly HTMLElement[]): number {
    const visible = elements.filter((element) => element.offsetHeight > 0);
    if (visible.length === 0) {
      return 0;
    }

    const last = visible[visible.length - 1];
    return this.sheetOffsetTop(last) + last.offsetHeight - this.sheetOffsetTop(origin);
  }

  /** Move a lead that fits on one page when it does not fit in the room left. */
  private pushTogether(element: HTMLElement, blockHeight: number, padTop: number): boolean {
    const extra = cvPageBreakMargin(
      this.sheetOffsetTop(element),
      blockHeight,
      CV_A4_HEIGHT_PX,
      CV_PAGE_GAP_PX,
      padTop
    );
    if (extra <= 0.5) {
      return false;
    }

    element.style.marginTop = `${Math.round(extra)}px`;
    element.classList.add('cv-export-preview__page-break');
    return true;
  }

  private clearEntryMargins(entry: HTMLElement): void {
    this.clearSheetMargin(entry);
    const parts = this.entryParts(entry);
    [...parts.lead, ...parts.bullets, ...parts.trailing].forEach((line) => this.clearSheetMargin(line));
  }

  private placeOnSheet(element: HTMLElement, padTop: number): void {
    const applied = Number.parseFloat(element.style.marginTop) || 0;
    const naturalTop = this.sheetOffsetTop(element) - applied;
    const extra = cvPageBreakMargin(naturalTop, element.offsetHeight, CV_A4_HEIGHT_PX, CV_PAGE_GAP_PX, padTop);
    const next = extra > 0.5 ? `${Math.round(extra)}px` : '';

    element.classList.toggle('cv-export-preview__page-break', next.length > 0);

    if ((element.style.marginTop || '') !== next) {
      element.style.marginTop = next;
    }
  }

  /**
   * Distance from the sheet top. An Entry is positioned, so a bullet's own
   * offsetTop is not its position on the page.
   */
  private sheetOffsetTop(element: HTMLElement): number {
    const page = element.closest('.cv-export-preview__page');

    if (!(page instanceof HTMLElement)) {
      return element.offsetTop;
    }

    let top = 0;
    let current: HTMLElement | null = element;

    while (current instanceof HTMLElement && current !== page) {
      top += current.offsetTop;
      const parent: Element | null = current.offsetParent;

      if (!(parent instanceof HTMLElement) || parent === current || !page.contains(parent)) {
        break;
      }

      current = parent;
    }

    return top;
  }

  /** Drop a previous sheet move so the fit scale is measured on the natural flow. */
  private clearPageBreaks(page: HTMLElement): void {
    page.querySelectorAll<HTMLElement>('.cv-export-preview__page-break').forEach((element) => {
      this.clearSheetMargin(element);
    });
  }

  /**
   * Loosest scale whose content fits on one A4 sheet.
   * Probes from the tight end and restores the last fit before returning, so an
   * overflowing scale is never left on the page for the browser to paint as a second sheet.
   */
  private applyFitScale(page: HTMLElement): boolean {
    const scales = [...CV_CANVAS_FIT_SCALES].reverse();
    let fitted: number | null = null;

    for (const scale of scales) {
      page.style.setProperty('--cv-canvas-scale', String(scale));

      if (page.scrollHeight > CV_A4_HEIGHT_PX + 0.5) {
        if (fitted === null) {
          return false;
        }

        page.style.setProperty('--cv-canvas-scale', String(fitted));
        return true;
      }

      fitted = scale;
    }

    return true;
  }

  /** Page-1 top inset, repeated when a block starts on a later sheet. */
  private sheetPadTopPx(page: HTMLElement): number {
    const raw = getComputedStyle(page).getPropertyValue('--cv-sheet-pad-top').trim();
    if (!raw) {
      return 0;
    }

    const probe = page.ownerDocument.createElement('div');
    probe.style.position = 'absolute';
    probe.style.visibility = 'hidden';
    probe.style.pointerEvents = 'none';
    probe.style.height = '0';
    probe.style.margin = '0';
    probe.style.border = '0';
    probe.style.padding = '0';
    probe.style.paddingTop = raw;
    page.appendChild(probe);
    const pad = probe.offsetHeight;
    probe.remove();
    return pad;
  }

  private clearSheetMargin(element: HTMLElement): void {
    element.classList.remove('cv-export-preview__page-break');

    if (element.style.marginTop) {
      element.style.marginTop = '';
    }

    if (element.style.marginBottom) {
      element.style.marginBottom = '';
    }
  }
}
