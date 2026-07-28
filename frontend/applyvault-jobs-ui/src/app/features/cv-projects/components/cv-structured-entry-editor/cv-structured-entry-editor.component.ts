import { LowerCasePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import {
  CvSectionFieldCatalog,
  CvSectionType,
  CvStructuredEntry
} from '../../models/cv-structured.model';
import {
  fieldPlaceholder,
  patchEntryStringField,
  patchEntryStringListField,
  readEntryStringField,
  readEntryStringListField,
  shouldRenderFieldsInRow,
  stringListUsesCommaInput
} from '../../utils/cv-entry-fields.util';
import { CvMarkdownFieldComponent } from '../cv-markdown-field/cv-markdown-field.component';

@Component({
  selector: 'app-cv-structured-entry-editor',
  standalone: true,
  imports: [CvMarkdownFieldComponent, LowerCasePipe],
  templateUrl: './cv-structured-entry-editor.component.html',
  styleUrl: './cv-structured-entry-editor.component.scss'
})
export class CvStructuredEntryEditorComponent {
  readonly entry = input.required<CvStructuredEntry>();
  readonly sectionType = input.required<CvSectionType>();
  readonly entryFields = input.required<readonly CvSectionFieldCatalog[]>();
  readonly disabled = input(false);
  readonly canMoveUp = input(false);
  readonly canMoveDown = input(false);
  readonly entryIndex = input(0);
  readonly fieldRevision = input(0);

  readonly entryChange = output<Partial<CvStructuredEntry>>();
  readonly moveUp = output<void>();
  readonly moveDown = output<void>();
  readonly remove = output<void>();

  protected readonly shouldRenderFieldsInRow = shouldRenderFieldsInRow;
  protected readonly fieldPlaceholder = fieldPlaceholder;
  protected readonly stringListUsesCommaInput = stringListUsesCommaInput;

  protected readString(entry: CvStructuredEntry, field: CvSectionFieldCatalog): string {
    return readEntryStringField(entry, this.sectionType(), field);
  }

  protected readStringList(entry: CvStructuredEntry, field: CvSectionFieldCatalog): readonly string[] {
    return readEntryStringListField(entry, field);
  }

  protected updateString(field: CvSectionFieldCatalog, value: string): void {
    this.entryChange.emit(patchEntryStringField(this.sectionType(), field, value));
  }

  protected updateStringInput(field: CvSectionFieldCatalog, event: Event): void {
    this.updateString(field, readInputValue(event));
  }

  protected updateStringListItem(field: CvSectionFieldCatalog, index: number, value: string): void {
    const values = [...readEntryStringListField(this.entry(), field)];
    values[index] = value;
    this.entryChange.emit(patchEntryStringListField(field, values));
  }

  protected addStringListItem(field: CvSectionFieldCatalog): void {
    const values = [...readEntryStringListField(this.entry(), field), ''];
    this.entryChange.emit(patchEntryStringListField(field, values));
  }

  protected removeStringListItem(field: CvSectionFieldCatalog, index: number): void {
    const values = readEntryStringListField(this.entry(), field).filter((_, itemIndex) => itemIndex !== index);
    this.entryChange.emit(patchEntryStringListField(field, values));
  }

  protected updateCommaSeparatedList(field: CvSectionFieldCatalog, event: Event): void {
    this.updateString(field, readInputValue(event));
  }

  protected usesMarkdownInline(field: CvSectionFieldCatalog): boolean {
    return field.kind === 'string' && (field.id === 'title' || field.id === 'groupTitle');
  }

  protected usesPlainStringInput(field: CvSectionFieldCatalog): boolean {
    return field.kind === 'string'
      && field.id !== 'title'
      && field.id !== 'groupTitle'
      && field.id !== 'skills';
  }

  protected shouldSkipField(field: CvSectionFieldCatalog, index: number, fields: readonly CvSectionFieldCatalog[]): boolean {
    return field.id === 'dateRange' && index > 0 && fields[index - 1]?.id === 'subtitle';
  }

  protected dateRangeField(fields: readonly CvSectionFieldCatalog[]): CvSectionFieldCatalog | undefined {
    return fields.find((field) => field.id === 'dateRange');
  }
}
