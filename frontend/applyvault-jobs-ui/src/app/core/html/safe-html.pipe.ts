import { inject, Pipe, PipeTransform } from '@angular/core';
import { SafeHtml } from '@angular/platform-browser';

import { SafeHtmlService } from './safe-html.service';

@Pipe({
  name: 'safeHtml',
  standalone: true
})
export class SafeHtmlPipe implements PipeTransform {
  private readonly safeHtml = inject(SafeHtmlService);

  transform(value: string | null | undefined): SafeHtml {
    return this.safeHtml.sanitize(value);
  }
}
