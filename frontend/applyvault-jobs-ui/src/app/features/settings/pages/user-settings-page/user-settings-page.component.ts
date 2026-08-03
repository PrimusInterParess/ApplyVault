import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { SkeletonBlockComponent } from '../../../../shared/ui/skeleton-block.component';
import { CalendarConnectionsFacade } from '../../data-access/calendar-connections.facade';
import { GitHubConnectionsFacade } from '../../data-access/github-connections.facade';
import { MailConnectionsFacade } from '../../data-access/mail-connections.facade';
import { ConnectedCalendarAccount } from '../../models/calendar-connection.model';
import { ConnectedGitHubAccount } from '../../models/github-connection.model';
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
  readonly kind: 'calendar' | 'mail' | 'github';
  readonly connectionId: string;
  readonly provider?: string;
}

interface SettingsHelpCopy {
  readonly title: string;
  readonly lead: string;
  readonly detail: string;
}

type SettingsHelpKey = string;

const SETTINGS_HELP_COPY: Readonly<Record<string, SettingsHelpCopy>> = {
  'calendar:connect:google': {
    title: 'Google Calendar',
    lead: 'Create interview events on Google Calendar',
    detail:
      'Opens Google sign-in so ApplyVault can add interview events from your saved jobs to this calendar account.'
  },
  'calendar:connect:microsoft': {
    title: 'Microsoft Calendar',
    lead: 'Create interview events on Outlook calendar',
    detail:
      'Opens Microsoft sign-in so ApplyVault can add interview events from your saved jobs to this Outlook calendar account.'
  },
  'calendar:refresh': {
    title: 'Refresh calendars',
    lead: 'Reload connected calendar accounts',
    detail:
      'Fetches the latest connection status and expiry for Google and Microsoft calendars. Does not create events.'
  },
  'calendar:disconnect': {
    title: 'Disconnect calendar',
    lead: 'Stop using this calendar for interview events',
    detail: 'Removes this account from ApplyVault. You can reconnect later. Confirm in the next step.'
  },
  'github:connect': {
    title: 'GitHub',
    lead: 'Repository and profile access',
    detail:
      'Opens GitHub sign-in so ApplyVault can read your public profile and repos for portfolio / CV projects.'
  },
  'github:refresh': {
    title: 'Refresh GitHub',
    lead: 'Reload GitHub connection',
    detail:
      'Fetches the latest GitHub connection status. Does not change which repos are imported.'
  },
  'github:disconnect': {
    title: 'Disconnect GitHub',
    lead: 'Remove GitHub from ApplyVault',
    detail:
      'Stops repo/profile access. Synced projects will be removed from ApplyVault. Confirm in the next step.'
  },
  'mail:connect:gmail': {
    title: 'Gmail',
    lead: 'Background sync for job status emails',
    detail:
      'Opens Google sign-in for Gmail so ApplyVault can detect rejection and interview emails about your saved jobs. Does not send mail. Outlook mailbox is not supported.'
  },
  'mail:refresh': {
    title: 'Refresh Gmail',
    lead: 'Reload mailbox connection',
    detail: 'Fetches the latest Gmail connection and sync status. Does not send email.'
  },
  'mail:disconnect': {
    title: 'Disconnect Gmail',
    lead: 'Stop mailbox sync',
    detail:
      'Stops interview/rejection email detection for saved jobs. Confirm in the next step.'
  }
};

const EMPTY_HELP_COPY: SettingsHelpCopy = {
  title: '',
  lead: '',
  detail: ''
};

@Component({
  selector: 'app-user-settings-page',
  standalone: true,
  imports: [CommonModule, DatePipe, SkeletonBlockComponent],
  templateUrl: './user-settings-page.component.html',
  styleUrl: './user-settings-page.component.scss'
})
export class UserSettingsPageComponent {
  protected readonly calendarConnections = inject(CalendarConnectionsFacade);
  protected readonly gitHubConnections = inject(GitHubConnectionsFacade);
  protected readonly mailConnections = inject(MailConnectionsFacade);
  protected readonly skeletonRowIndexes = [0, 1, 2] as const;
  protected readonly disconnectConfirm = signal<DisconnectConfirmTarget | null>(null);

  /** While set, hide that control's hover/focus popover until the pointer leaves. */
  protected readonly suppressedHelpKey = signal<SettingsHelpKey | null>(null);

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

  protected gitHubSectionStatus(): StatusPresentation {
    const connections = this.gitHubConnections.connections();

    if (this.gitHubConnections.loading()) {
      return { label: 'Loading', variant: 'neutral' };
    }

    if (connections.length === 0) {
      return { label: 'Not connected', variant: 'neutral' };
    }

    return { label: 'Connected', variant: 'success' };
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
      case 'github':
        return 'GitHub';
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
      case 'github':
        return 'GH';
      default:
        return provider.trim().charAt(0).toUpperCase() || '?';
    }
  }

  protected providerBadgeClass(provider: string): string {
    const normalized = provider.trim().toLowerCase();

    return `settings-page__provider-badge settings-page__provider-badge--${normalized || 'unknown'}`;
  }

  protected connectionDisplayName(
    connection: ConnectedCalendarAccount | ConnectedMailAccount | ConnectedGitHubAccount
  ): string {
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

  protected helpId(key: SettingsHelpKey): string {
    return `settings-help-${key.replace(/:/g, '-')}`;
  }

  protected helpCopyFor(key: SettingsHelpKey): SettingsHelpCopy {
    const normalizedKey = key.replace(/:disconnect:[^:]+$/, ':disconnect');
    return SETTINGS_HELP_COPY[normalizedKey] ?? SETTINGS_HELP_COPY[key] ?? EMPTY_HELP_COPY;
  }

  protected isHelpSuppressed(key: SettingsHelpKey): boolean {
    return this.suppressedHelpKey() === key;
  }

  protected onHelpMouseLeave(key: SettingsHelpKey): void {
    if (this.suppressedHelpKey() === key) {
      this.suppressedHelpKey.set(null);
    }
  }

  protected onCalendarConnect(provider: 'google' | 'microsoft', event: Event): void {
    this.dismissHelp(`calendar:connect:${provider}`, event);
    this.calendarConnections.connect(provider);
  }

  protected onCalendarRefresh(event: Event): void {
    this.dismissHelp('calendar:refresh', event);
    this.calendarConnections.load();
  }

  protected onGitHubConnect(event: Event): void {
    this.dismissHelp('github:connect', event);
    this.gitHubConnections.connect();
  }

  protected onGitHubRefresh(event: Event): void {
    this.dismissHelp('github:refresh', event);
    this.gitHubConnections.load();
  }

  protected onMailConnect(event: Event): void {
    this.dismissHelp('mail:connect:gmail', event);
    this.mailConnections.connect('gmail');
  }

  protected onMailRefresh(event: Event): void {
    this.dismissHelp('mail:refresh', event);
    this.mailConnections.load();
  }

  protected beginDisconnectCalendar(connectionId: string, provider: string, event?: Event): void {
    if (event) {
      this.dismissHelp(`calendar:disconnect:${connectionId}`, event);
    }

    this.disconnectConfirm.set({
      kind: 'calendar',
      connectionId,
      provider
    });
  }

  protected beginDisconnectMail(connectionId: string, event?: Event): void {
    if (event) {
      this.dismissHelp(`mail:disconnect:${connectionId}`, event);
    }

    this.disconnectConfirm.set({
      kind: 'mail',
      connectionId
    });
  }

  protected beginDisconnectGitHub(connectionId: string, event?: Event): void {
    if (event) {
      this.dismissHelp(`github:disconnect:${connectionId}`, event);
    }

    this.disconnectConfirm.set({
      kind: 'github',
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

    if (target.kind === 'github') {
      return 'Disconnect GitHub? Synced projects will be removed from ApplyVault.';
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
    } else if (target.kind === 'github') {
      this.gitHubConnections.disconnect(target.connectionId);
    } else {
      this.mailConnections.disconnect(target.connectionId);
    }

    this.disconnectConfirm.set(null);
  }

  protected cancelDisconnectConfirm(): void {
    this.disconnectConfirm.set(null);
  }

  private dismissHelp(key: SettingsHelpKey, event: Event): void {
    this.suppressedHelpKey.set(key);

    const target = event.currentTarget;
    if (target instanceof HTMLElement) {
      target.blur();
    }
  }

  private isMailSyncIssue(syncStatus: string | null | undefined): boolean {
    const normalized = syncStatus?.trim().toLowerCase() ?? '';

    return normalized === 'error' || normalized === 'needs_reconnect';
  }
}
