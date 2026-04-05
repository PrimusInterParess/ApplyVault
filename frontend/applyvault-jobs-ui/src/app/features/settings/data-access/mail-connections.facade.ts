import { effect, inject, Injectable, signal } from '@angular/core';
import { Subscription } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { ConnectedMailAccount } from '../models/mail-connection.model';
import { MailConnectionsApiService } from './mail-connections-api.service';

@Injectable({ providedIn: 'root' })
export class MailConnectionsFacade {
  private readonly authService = inject(AuthService);
  private readonly apiService = inject(MailConnectionsApiService);
  private loadSubscription: Subscription | null = null;
  private loadedUserId: string | null = null;

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly connectingProvider = signal<string | null>(null);
  readonly disconnectingConnectionId = signal<string | null>(null);
  readonly connections = signal<readonly ConnectedMailAccount[]>([]);

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
          this.cancelPendingLoad();
          this.resetState();
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
        this.error.set('Mail connections could not be loaded.');
        this.connections.set([]);
        this.loading.set(false);
        this.loadSubscription = null;
      }
    });
  }

  connect(provider: string): void {
    if (this.connectingProvider() === provider) {
      return;
    }

    this.connectingProvider.set(provider);
    this.error.set(null);

    this.apiService.startConnection(provider).subscribe({
      next: (response) => {
        window.location.assign(response.authorizationUrl);
      },
      error: () => {
        this.error.set(`The ${provider} Gmail connection flow could not be started.`);
        this.connectingProvider.set(null);
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
        this.error.set('The Gmail connection could not be removed.');
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
    this.connectingProvider.set(null);
    this.disconnectingConnectionId.set(null);
    this.connections.set([]);
  }
}
