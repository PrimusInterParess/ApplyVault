import { CommonModule, TitleCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-github-connect-callback',
  standalone: true,
  imports: [CommonModule, RouterLink, TitleCasePipe],
  templateUrl: './github-connect-callback.component.html',
  styleUrl: './github-connect-callback.component.scss'
})
export class GitHubConnectCallbackComponent {
  private readonly route = inject(ActivatedRoute);

  readonly provider = this.route.snapshot.queryParamMap.get('provider') ?? 'github';
  readonly success = this.route.snapshot.queryParamMap.get('success') === 'true';
}
