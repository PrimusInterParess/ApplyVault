import { inject, Injectable, SecurityContext } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Injectable({ providedIn: 'root' })
export class SafeHtmlService {
  private readonly sanitizer = inject(DomSanitizer);

  /** Treat all API/user HTML as untrusted; sanitize before render. */
  sanitize(value: string | null | undefined): SafeHtml {
    const cleaned = this.sanitizer.sanitize(SecurityContext.HTML, value ?? '') ?? '';
    return this.sanitizer.bypassSecurityTrustHtml(cleaned);
  }
}
