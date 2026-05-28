import { Component, effect, ElementRef, input, output, viewChild } from '@angular/core';

import { readInputValue } from '../../../../core/dom/input-value.util';
import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import {
  prefixMarkdownLine,
  wrapMarkdownSelection
} from '../../utils/markdown-selection.util';
import { renderInlineMarkdown, renderMarkdown } from '../../utils/markdown.util';

@Component({
  selector: 'app-cv-markdown-field',
  standalone: true,
  imports: [SafeHtmlPipe],
  templateUrl: './cv-markdown-field.component.html',
  styleUrl: './cv-markdown-field.component.scss'
})
export class CvMarkdownFieldComponent {
  readonly value = input('');
  readonly rows = input(4);
  readonly placeholder = input('');
  readonly disabled = input(false);
  readonly previewMode = input<'inline' | 'block'>('inline');
  readonly label = input<string | null>(null);
  readonly showToolbar = input(true);
  readonly fieldRevision = input(0);

  readonly valueChange = output<string>();

  private readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('textarea');

  protected readonly renderInlineMarkdown = renderInlineMarkdown;
  protected readonly renderMarkdown = renderMarkdown;

  constructor() {
    effect(() => {
      this.fieldRevision();
      this.syncTextareaValue(this.value());
    });
  }

  protected onInput(event: Event): void {
    this.valueChange.emit(readInputValue(event));
  }

  protected applyBold(): void {
    this.applyWrap('bold');
  }

  protected applyItalic(): void {
    this.applyWrap('italic');
  }

  protected applyLink(): void {
    this.applyWrap('link');
  }

  protected applyBulletPrefix(): void {
    this.applyPrefix('- ');
  }

  private applyWrap(kind: 'bold' | 'italic' | 'link'): void {
    const textarea = this.textarea()?.nativeElement;

    if (!textarea || this.disabled()) {
      return;
    }

    const edit = wrapMarkdownSelection(
      this.value(),
      textarea.selectionStart,
      textarea.selectionEnd,
      kind
    );

    this.emitAndRestoreSelection(edit.value, edit.selectionStart, edit.selectionEnd);
  }

  private applyPrefix(prefix: string): void {
    const textarea = this.textarea()?.nativeElement;

    if (!textarea || this.disabled()) {
      return;
    }

    const edit = prefixMarkdownLine(
      this.value(),
      textarea.selectionStart,
      textarea.selectionEnd,
      prefix
    );

    this.emitAndRestoreSelection(edit.value, edit.selectionStart, edit.selectionEnd);
  }

  private emitAndRestoreSelection(value: string, selectionStart: number, selectionEnd: number): void {
    this.syncTextareaValue(value);
    this.valueChange.emit(value);

    queueMicrotask(() => {
      const textarea = this.textarea()?.nativeElement;

      if (!textarea) {
        return;
      }

      textarea.focus();
      textarea.setSelectionRange(selectionStart, selectionEnd);
    });
  }

  private syncTextareaValue(value: string): void {
    const textarea = this.textarea()?.nativeElement;

    if (!textarea || textarea.value === value) {
      return;
    }

    textarea.value = value;
  }
}
