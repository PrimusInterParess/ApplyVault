import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CalendarConnectionsFacade } from '../../data-access/calendar-connections.facade';
import { MailConnectionsFacade } from '../../data-access/mail-connections.facade';
import { ConnectedCalendarAccount } from '../../models/calendar-connection.model';
import { ConnectedMailAccount } from '../../models/mail-connection.model';

type StatusVariant = 'neutral' | 'success' | 'warning' | 'danger';

interface StatusPresentation {
  readonly label: string;
  readonly variant: StatusVariant;
}

interface ExpiryPresentation {
  readonly label: string;
  readonly variant: StatusVariant;
}

interface DisconnectConfirmTarget {
  readonly kind: 'calendar' | 'mail';
  readonly connectionId: string;
  readonly provider?: string;
}

@Component({
  selector: 'app-user-settings-page',
  standalone: true,
  imports: [CommonModule, DatePipe, SkeletonBlockComponent],
  templateUrl: './user-settings-page.component.html',
  styleUrl: './user-settings-page.component.scss'
})
export class UserSettingsPageComponent {
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);
  protected readonly mailConnections = inject(MailConnectionsFacade);
  protected readonly skeletonRowIndexes = [0, 1, 2] as const;
  protected readonly disconnectConfirm = signal<DisconnectConfirmTarget | null>(null);

  protected isCalendarProviderConnected(provider: string): boolean {
    const normalizedProvider = provider.trim().toLowerCase();

    return this.calendarConnections
      .connections()
      .some((connection) => connection.provider.trim().toLowerCase() === normalizedProvider);
  }

  protected isMailProviderConnected(provider: string): boolean {
    const normalizedProvider = provider.trim().toLowerCase();

    return this.mailConnections
      .connections()
      .some((connection) => connection.provider.trim().toLowerCase() === normalizedProvider);
  }

  protected calendarSectionStatus(): StatusPresentation {
    const count = this.calendarConnections.connections().length;

    if (this.calendarConnections.loading()) {
      return { label: 'Loading', variant: 'neutral' };
    }

    if (count === 0) {
      return { label: 'Not connected', variant: 'neutral' };
    }

    if (count === 1) {
      return { label: '1 connected', variant: 'success' };
    }

    return { label: `${count} connected`, variant: 'success' };
  }

  protected mailSectionStatus(): StatusPresentation {
    const connections = this.mailConnections.connections();

    if (this.mailConnections.loading()) {
      return { label: 'Loading', variant: 'neutral' };
    }

    if (connections.length === 0) {
      return { label: 'Not connected', variant: 'neutral' };
    }

    const hasSyncIssue = connections.some(
      (connection) =>
        this.isMailSyncIssue(connection.syncStatus) || !!connection.lastSyncError?.trim()
    );

    if (hasSyncIssue) {
      return { label: 'Sync issue', variant: 'warning' };
    }

    return { label: 'Connected', variant: 'success' };
  }

  protected syncStatusPresentation(value: string | null | undefined): StatusPresentation {
    const normalized = value?.trim().toLowerCase() ?? '';

    switch (normalized) {
      case 'connected':
        return { label: 'Connected', variant: 'success' };
      case 'syncing':
        return { label: 'Syncing', variant: 'neutral' };
      case 'error':
        return { label: 'Error', variant: 'danger' };
      case 'needs_reconnect':
        return { label: 'Needs reconnect', variant: 'warning' };
      default:
        return {
          label: this.formatSyncStatus(value),
          variant: 'neutral'
        };
    }
  }

  protected expiryPresentation(expiresAt: string | null | undefined): ExpiryPresentation | null {
    if (!expiresAt?.trim()) {
      return null;
    }

    const expiryDate = new Date(expiresAt);

    if (Number.isNaN(expiryDate.getTime())) {
      return null;
    }

    const now = Date.now();
    const millisecondsUntilExpiry = expiryDate.getTime() - now;

    if (millisecondsUntilExpiry <= 0) {
      return { label: 'Expired', variant: 'danger' };
    }

    const sevenDaysInMs = 7 * 24 * 60 * 60 * 1000;

    if (millisecondsUntilExpiry <= sevenDaysInMs) {
      return { label: 'Reconnect soon', variant: 'warning' };
    }

    return null;
  }

  protected providerLabel(provider: string): string {
    const normalized = provider.trim().toLowerCase();

    switch (normalized) {
      case 'google':
        return 'Google';
      case 'microsoft':
        return 'Microsoft';
      case 'gmail':
        return 'Gmail';
      default:
        return provider
          .replace(/[_-]+/g, ' ')
          .replace(/\b\w/g, (character) => character.toUpperCase());
    }
  }

  protected providerInitial(provider: string): string {
    const normalized = provider.trim().toLowerCase();

    switch (normalized) {
      case 'google':
        return 'G';
      case 'microsoft':
        return 'M';
      case 'gmail':
        return '@';
      default:
        return provider.trim().charAt(0).toUpperCase() || '?';
    }
  }

  protected providerBadgeClass(provider: string): string {
    const normalized = provider.trim().toLowerCase();

    return `settings-page__provider-badge settings-page__provider-badge--${normalized || 'unknown'}`;
  }

  protected connectionDisplayName(connection: ConnectedCalendarAccount | ConnectedMailAccount): string {
    return connection.displayName || connection.email || connection.providerUserId;
  }

  protected formatSyncStatus(value: string | null | undefined): string {
    const normalized = value?.trim();

    if (!normalized) {
      return 'Unknown';
    }

    return normalized
      .replace(/[_-]+/g, ' ')
      .replace(/\b\w/g, (character) => character.toUpperCase());
  }

  protected beginDisconnectCalendar(connectionId: string, provider: string): void {
    this.disconnectConfirm.set({
      kind: 'calendar',
      connectionId,
      provider
    });
  }

  protected beginDisconnectMail(connectionId: string): void {
    this.disconnectConfirm.set({
      kind: 'mail',
      connectionId
    });
  }

  protected disconnectConfirmMessage(): string {
    const target = this.disconnectConfirm();

    if (!target) {
      return '';
    }

    if (target.kind === 'calendar') {
      return `Disconnect ${this.providerLabel(target.provider ?? '')}? Interview event sync will stop using this account.`;
    }

    return 'Disconnect Gmail? Interview and rejection email sync will stop for your saved jobs.';
  }

  protected confirmDisconnect(): void {
    const target = this.disconnectConfirm();

    if (!target) {
      return;
    }

    if (target.kind === 'calendar') {
      this.calendarConnections.disconnect(target.connectionId);
    } else {
      this.mailConnections.disconnect(target.connectionId);
    }

    this.disconnectConfirm.set(null);
  }

  protected cancelDisconnectConfirm(): void {
    this.disconnectConfirm.set(null);
  }

  private isMailSyncIssue(syncStatus: string | null | undefined): boolean {
    const normalized = syncStatus?.trim().toLowerCase() ?? '';

    return normalized === 'error' || normalized === 'needs_reconnect';
  }
}
