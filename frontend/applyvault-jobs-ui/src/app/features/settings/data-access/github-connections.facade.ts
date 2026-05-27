import { effect, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { GitHubConnectionsApiService } from './github-connections-api.service';
import { ConnectedGitHubAccount } from '../models/github-connection.model';

@Injectable({ providedIn: 'root' })
export class GitHubConnectionsFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(GitHubConnectionsApiService);
  private loadSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly connecting = signal(false);
  readonly disconnectingConnectionId = signal<string | null>(null);
  readonly connections = signal<readonly ConnectedGitHubAccount[]>([]);

  constructor() {
    effect(
      () => {
        const session = this.authService.session();
        const currentUserId = this.authService.currentUser()?.id ?? null;

        if (!session) {
          this.loadedUserId = null;
          this.cancelPendingLoad();
          this.resetState();
          return;
        }

        if (!currentUserId) {
          this.loading.set(true);
          return;
        }

        if (this.loadedUserId === currentUserId) {
          return;
        }

        this.loadedUserId = currentUserId;
        this.resetState();
        this.load();
      },
      { allowSignalWrites: true }
    );
  }

  load(): void {
    this.cancelPendingLoad();
    this.loading.set(true);
    this.error.set(null);

    this.loadSubscription = this.apiService.getAll().subscribe({
      next: (connections) => {
        this.connections.set(connections);
        this.loading.set(false);
        this.loadSubscription = null;
      },
      error: () => {
        this.error.set('GitHub connections could not be loaded.');
        this.connections.set([]);
        this.loading.set(false);
        this.loadSubscription = null;
      }
    });
  }

  connect(): void {
    if (this.connecting()) {
      return;
    }

    this.connecting.set(true);
    this.error.set(null);

    this.apiService.startConnection('github').subscribe({
      next: (response) => {
        window.location.assign(response.authorizationUrl);
      },
      error: () => {
        this.error.set('The GitHub connection flow could not be started.');
        this.connecting.set(false);
      }
    });
  }

  disconnect(id: string): void {
    if (this.disconnectingConnectionId() === id) {
      return;
    }

    this.disconnectingConnectionId.set(id);
    this.error.set(null);

    this.apiService.deleteConnection(id).subscribe({
      next: () => {
        this.connections.update((connections) => connections.filter((connection) => connection.id !== id));
        this.disconnectingConnectionId.set(null);
      },
      error: () => {
        this.error.set('The GitHub connection could not be removed.');
        this.disconnectingConnectionId.set(null);
      }
    });
  }

  private cancelPendingLoad(): void {
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = null;
  }

  private resetState(): void {
    this.loading.set(false);
    this.error.set(null);
    this.connecting.set(false);
    this.disconnectingConnectionId.set(null);
    this.connections.set([]);
  }
}
