import {
  Directive,
  ElementRef,
  HostBinding,
  HostListener,
  inject,
  input,
  OnChanges,
  output
} from '@angular/core';

import { renderInlineField, serializeInlineField } from '../utils/cv-inline-link.util';

@Directive({
  selector: '[appInlineEditableText]',
  standalone: true
})
export class InlineEditableTextDirective implements OnChanges {
  private readonly element = inject(ElementRef<HTMLElement>);

  readonly text = input('', { alias: 'appInlineEditableText' });
  readonly enabled = input(false);
  readonly inlineLinks = input(true);
  readonly textChange = output<string>();

  @HostBinding('attr.data-cv-inline-links')
  protected get inlineLinksAttr(): string | null {
    return this.inlineLinks() ? null : 'false';
  }

  @HostBinding('attr.contenteditable')
  protected get contentEditableAttr(): string | null {
    return this.enabled() ? 'true' : null;
  }

  ngOnChanges(): void {
    const element = this.element.nativeElement;

    if (document.activeElement === element) {
      return;
    }

    if (!this.inlineLinks()) {
      element.textContent = this.text();
      return;
    }

    if (serializeInlineField(element) === this.text()) {
      return;
    }

    renderInlineField(element, this.text());
  }

  @HostListener('blur')
  protected onBlur(): void {
    if (!this.enabled()) {
      return;
    }

    const next = this.inlineLinks()
      ? serializeInlineField(this.element.nativeElement).trim()
      : (this.element.nativeElement.textContent?.trim() ?? '');

    if (next === this.text().trim()) {
      return;
    }

    this.textChange.emit(next);
  }

  @HostListener('cv-inline-link', ['$event'])
  protected onInlineLink(event: Event): void {
    if (!this.enabled() || !this.inlineLinks()) {
      return;
    }

    const next = (event as CustomEvent<string>).detail;

    if (typeof next !== 'string') {
      return;
    }

    event.stopPropagation();
    const trimmed = next.trim();
    renderInlineField(this.element.nativeElement, trimmed);

    if (trimmed !== this.text().trim()) {
      this.textChange.emit(trimmed);
    }
  }

  @HostListener('click', ['$event'])
  protected onClick(event: MouseEvent): void {
    const target = event.target;

    if (!(target instanceof Element) || !target.closest('a')) {
      return;
    }

    if (event.metaKey || event.ctrlKey) {
      return;
    }

    event.preventDefault();
  }

  @HostListener('keydown.enter', ['$event'])
  protected onEnter(event: Event): void {
    if (!this.enabled()) {
      return;
    }

    const keyboardEvent = event as KeyboardEvent;
    const tagName = this.element.nativeElement.tagName;

    if (tagName === 'P' || tagName === 'LI' || tagName === 'DIV') {
      return;
    }

    keyboardEvent.preventDefault();
    this.element.nativeElement.blur();
  }
}
