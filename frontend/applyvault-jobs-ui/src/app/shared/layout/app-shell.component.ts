import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss'
})
export class AppShellComponent implements AfterViewInit, OnDestroy {
  protected readonly auth = inject(AuthService);

  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('shellHeader', { read: ElementRef })
  private readonly shellHeader?: ElementRef<HTMLElement>;

  protected readonly moreMenuOpen = signal(false);

  private headerResizeObserver: ResizeObserver | null = null;

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.closeMoreMenu());
  }

  ngAfterViewInit(): void {
    const headerEl = this.shellHeader?.nativeElement;
    if (!headerEl || typeof ResizeObserver === 'undefined') {
      this.syncHeaderOffset();
      return;
    }

    this.headerResizeObserver = new ResizeObserver(() => this.syncHeaderOffset());
    this.headerResizeObserver.observe(headerEl);
    this.syncHeaderOffset();
  }

  ngOnDestroy(): void {
    this.headerResizeObserver?.disconnect();
    this.headerResizeObserver = null;
    document.documentElement.style.removeProperty('--app-header-offset');
    document.documentElement.style.removeProperty('--app-header-height');
  }

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/login');
  }

  protected toggleMoreMenu(): void {
    this.moreMenuOpen.update((open) => !open);
  }

  protected closeMoreMenu(): void {
    if (this.moreMenuOpen()) {
      this.moreMenuOpen.set(false);
    }
  }

  @HostListener('document:keydown', ['$event'])
  protected onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.closeMoreMenu();
    }
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.moreMenuOpen()) {
      return;
    }

    const headerEl = this.shellHeader?.nativeElement;
    const target = event.target;
    if (headerEl && target instanceof Node && !headerEl.contains(target)) {
      this.closeMoreMenu();
    }
  }

  private syncHeaderOffset(): void {
    const headerEl = this.shellHeader?.nativeElement;
    if (!headerEl) {
      return;
    }

    // Measure sticky chrome only. Shell min-height uses a stable CSS value — do not
    // feed this measurement back into layout min-height (locks tall chrome after wrap).
    const heightPx = `${Math.round(headerEl.getBoundingClientRect().height)}px`;
    document.documentElement.style.setProperty('--app-header-offset', heightPx);
    // Legacy sticky/drawer consumers still read --app-header-height.
    document.documentElement.style.setProperty('--app-header-height', heightPx);
  }
}
