import { CommonModule, TitleCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-mail-connect-callback',
  standalone: true,
  imports: [CommonModule, RouterLink, TitleCasePipe],
  templateUrl: './mail-connect-callback.component.html',
  styleUrl: './mail-connect-callback.component.scss'
})
export class MailConnectCallbackComponent {
  private readonly route = inject(ActivatedRoute);

  readonly provider = this.route.snapshot.queryParamMap.get('provider') ?? 'gmail';
  readonly success = this.route.snapshot.queryParamMap.get('success') === 'true';
}
