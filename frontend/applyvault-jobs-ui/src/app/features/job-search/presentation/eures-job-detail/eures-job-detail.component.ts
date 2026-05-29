import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SafeHtmlPipe } from '../../../../core/html/safe-html.pipe';
import { EuresJobDetail } from '../../models/eures-job.model';
import { formatEuresPublicationDate } from '../../utils/eures-date.util';

@Component({
  selector: 'app-eures-job-detail',
  standalone: true,
  imports: [DatePipe, SafeHtmlPipe, RouterLink],
  templateUrl: './eures-job-detail.component.html',
  styleUrl: './eures-job-detail.component.scss'
})
export class EuresJobDetailComponent {
  readonly job = input<EuresJobDetail | null>(null);
  readonly detailLoading = input(false);
  readonly detailError = input<string | null>(null);
  readonly saving = input(false);
  readonly saveError = input<string | null>(null);
  readonly savedJobId = input<string | null>(null);
  readonly saveAlreadyExists = input(false);
  readonly detailUrl = input<string | null>(null);
  readonly save = output<void>();
  readonly retryDetail = output<void>();

  protected readonly postedAt = computed(() => {
    const job = this.job();
    return job ? formatEuresPublicationDate(job.publicationDate) : null;
  });

  protected readonly canSave = computed(() => {
    return Boolean(this.job()) && !this.saving() && !this.savedJobId();
  });
}
