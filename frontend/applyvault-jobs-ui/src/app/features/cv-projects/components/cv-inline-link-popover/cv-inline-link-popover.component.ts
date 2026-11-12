import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  effect,
  inject,
  signal,
  viewChild
} from '@angular/core';

import {
  CV_INLINE_LINK_EVENT,
  CvInlineLinkSelection,
  captureInlineLinkSelection,
  removeInlineLink,
  replaceInlineLink,
  wrapInlineLink
} from '../../utils/cv-inline-link.util';

@Component({
  selector: 'app-cv-inline-link-popover',
  standalone: true,
  templateUrl: './cv-inline-link-popover.component.html',
  styleUrl: './cv-inline-link-popover.component.scss',
  host: {
    '[class.open]': 'open()',
    '[style.top.px]': 'top()',
    '[style.left.px]': 'left()',
    role: 'dialog',
    '[attr.aria-label]': 'editingExisting() ? "Edit link" : "Link selected words"'
  }
})
export class CvInlineLinkPopoverComponent implements OnInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly urlInput = viewChild<ElementRef<HTMLInputElement>>('urlInput');

  protected readonly open = signal(false);
  protected readonly composing = signal(false);
  protected readonly url = signal('');
  protected readonly error = signal('');
  protected readonly editingExisting = signal(false);
  protected readonly top = signal(0);
  protected readonly left = signal(0);

  private draft: CvInlineLinkSelection | null = null;

  constructor() {
    effect(() => {
      if (!this.composing()) {
        return;
      }

      const input = this.urlInput()?.nativeElement;

      if (!input) {
        return;
      }

      queueMicrotask(() => input.focus());
    });
  }

  ngOnInit(): void {
    document.body.appendChild(this.host.nativeElement);
    document.addEventListener('scroll', this.onScroll, true);
  }

  ngOnDestroy(): void {
    document.removeEventListener('scroll', this.onScroll, true);
    this.host.nativeElement.remove();
  }

  @HostListener('document:selectionchange')
  protected onSelectionChange(): void {
    if (this.composing()) {
      return;
    }

    const next = captureInlineLinkSelection();

    if (!next) {
      this.open.set(false);
      this.draft = null;
      return;
    }

    this.draft = next;
    this.editingExisting.set(next.link !== null);
    this.open.set(true);
    this.place(next.rect);
  }

  @HostListener('document:keydown', ['$event'])
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && this.open()) {
      event.preventDefault();
      this.close();
      return;
    }

    if (!(event.metaKey || event.ctrlKey) || event.key.toLowerCase() !== 'k' || this.composing()) {
      return;
    }

    const next = captureInlineLinkSelection();

    if (!next) {
      return;
    }

    event.preventDefault();
    this.begin(next);
  }

  @HostListener('document:mousedown', ['$event'])
  protected onDocumentMouseDown(event: MouseEvent): void {
    if (!this.composing()) {
      return;
    }

    if (event.target instanceof Node && this.host.nativeElement.contains(event.target)) {
      return;
    }

    this.close();
  }

  protected beginFromButton(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const next = captureInlineLinkSelection() ?? this.draft;

    if (!next) {
      return;
    }

    this.begin(next);
  }

  protected apply(event: Event): void {
    event.preventDefault();
    const draft = this.draft;

    if (!draft) {
      return;
    }

    const next = draft.link
      ? replaceInlineLink(draft.markdown, draft.link, this.url())
      : wrapInlineLink(draft.markdown, draft.start, draft.end, this.url());

    if (!next) {
      this.error.set('Use a web address or email.');
      return;
    }

    this.publish(draft.field, next);
    this.close();
  }

  protected remove(): void {
    const draft = this.draft;

    if (!draft?.link) {
      return;
    }

    this.publish(draft.field, removeInlineLink(draft.markdown, draft.link));
    this.close();
  }

  protected onUrlInput(event: Event): void {
    const value = event.target instanceof HTMLInputElement ? event.target.value : '';
    this.url.set(value);
    this.error.set('');
  }

  private begin(next: CvInlineLinkSelection): void {
    this.draft = next;
    this.editingExisting.set(next.link !== null);
    this.url.set(next.link?.href ?? '');
    this.error.set('');
    this.open.set(true);
    this.composing.set(true);
    this.place(next.rect);
  }

  private close(): void {
    this.composing.set(false);
    this.open.set(false);
    this.draft = null;
    this.error.set('');
    this.url.set('');
  }

  private publish(field: HTMLElement, markdown: string): void {
    field.dispatchEvent(new CustomEvent<string>(CV_INLINE_LINK_EVENT, { detail: markdown }));
  }

  private place(rect: DOMRect): void {
    const width = this.composing() ? 320 : 88;
    const left = Math.max(8, Math.min(rect.left, window.innerWidth - width - 8));
    let top = rect.top - (this.composing() ? 52 : 38);

    if (top < 8) {
      top = rect.bottom + 6;
    }

    this.left.set(left);
    this.top.set(top);
  }

  private readonly onScroll = (): void => {
    if (!this.open()) {
      return;
    }

    if (this.composing()) {
      const field = this.draft?.field;

      if (field) {
        this.place(field.getBoundingClientRect());
      }

      return;
    }

    const next = captureInlineLinkSelection();

    if (!next) {
      this.open.set(false);
      this.draft = null;
      return;
    }

    this.draft = next;
    this.place(next.rect);
  };
}
