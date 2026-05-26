import { Component, input } from '@angular/core';

@Component({
  selector: 'app-skeleton-block',
  standalone: true,
  templateUrl: './skeleton-block.component.html',
  styleUrl: './skeleton-block.component.scss'
})
export class SkeletonBlockComponent {
  readonly width = input('100%');
  readonly height = input('1rem');
  readonly rounded = input(false);
}
