import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';

@Component({
  selector: 'app-not-found-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './not-found-page.component.html',
  styleUrl: './not-found-page.component.scss'
})
export class NotFoundPageComponent implements OnInit {
  private readonly authService = inject(AuthService);

  readonly homeRoute = signal('/login');
  readonly homeLabel = signal('Go to sign in');

  async ngOnInit(): Promise<void> {
    await this.authService.ensureInitialized();

    if (this.authService.isAuthenticated()) {
      this.homeRoute.set('/jobs');
      this.homeLabel.set('Go to jobs');
    }
  }
}
