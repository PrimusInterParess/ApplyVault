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

  private pointerX = 0;
  private pointerY = 0;

  @HostListener('mousedown', ['$event'])
  protected onMouseDown(event: MouseEvent): void {
    this.pointerX = event.clientX;
    this.pointerY = event.clientY;
  }

  @HostListener('click', ['$event'])
  protected onClick(event: MouseEvent): void {
    const anchor = this.anchorFromClick(event);

    if (!anchor) {
      return;
    }

    const href = anchor.getAttribute('href');

    if (!href) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    const dragged = Math.hypot(event.clientX - this.pointerX, event.clientY - this.pointerY) > 4;

    if (dragged) {
      return;
    }

    window.open(href, '_blank', 'noopener,noreferrer');
  }

  private anchorFromClick(event: MouseEvent): HTMLAnchorElement | null {
    for (const node of event.composedPath()) {
      if (node instanceof HTMLAnchorElement && this.element.nativeElement.contains(node)) {
        return node;
      }
    }

    const raw = event.target;
    const element = raw instanceof Element ? raw : raw instanceof Node ? raw.parentElement : null;
    const anchor = element?.closest('a');

    return anchor instanceof HTMLAnchorElement && this.element.nativeElement.contains(anchor)
      ? anchor
      : null;
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
