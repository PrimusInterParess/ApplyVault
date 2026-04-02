import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';

type AuthMode = 'login' | 'signup';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.scss'
})
export class AuthPageComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly mode = signal<AuthMode>('login');
  readonly email = signal('');
  readonly password = signal('');
  readonly infoMessage = signal<string | null>(null);
  readonly auth = this.authService;

  switchMode(mode: AuthMode): void {
    this.mode.set(mode);
    this.infoMessage.set(null);
  }

  updateEmail(value: string): void {
    this.email.set(value);
  }

  updatePassword(value: string): void {
    this.password.set(value);
  }

  async submit(): Promise<void> {
    const email = this.email().trim();
    const password = this.password();

    if (!email || !password) {
      this.infoMessage.set('Email and password are required.');
      return;
    }

    this.infoMessage.set(null);

    try {
      if (this.mode() === 'login') {
        await this.authService.signIn(email, password);
      } else {
        const signUpMessage = await this.authService.signUp(email, password);

        if (signUpMessage) {
          this.infoMessage.set(signUpMessage);
          this.mode.set('login');
          return;
        }
      }

      await this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('redirectTo') || '/jobs');
    } catch {
      // AuthService surfaces the error state for the UI.
    }
  }
}
