import {
  Directive,
  ElementRef,
  HostListener,
  inject,
  input,
  OnChanges,
  output
} from '@angular/core';

@Directive({
  selector: '[appInlineEditableText]',
  standalone: true
})
export class InlineEditableTextDirective implements OnChanges {
  private readonly element = inject(ElementRef<HTMLElement>);

  readonly text = input('', { alias: 'appInlineEditableText' });
  readonly enabled = input(false);
  readonly textChange = output<string>();

  ngOnChanges(): void {
    const element = this.element.nativeElement;

    if (document.activeElement === element) {
      return;
    }

    element.textContent = this.text();
  }

  @HostListener('blur')
  protected onBlur(): void {
    if (!this.enabled()) {
      return;
    }

    this.textChange.emit(this.element.nativeElement.textContent?.trim() ?? '');
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
