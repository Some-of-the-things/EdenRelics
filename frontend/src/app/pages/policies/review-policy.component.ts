import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../services/seo.service';

/** How customer reviews work on the site (DMCC Act 2024 / CMA fake-reviews guidance). */
@Component({
  selector: 'app-review-policy',
  imports: [RouterLink],
  templateUrl: './review-policy.component.html',
  styleUrl: './privacy-policy.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Review Policy',
      description: 'How customer reviews work at Eden Relics: who can leave one, what we publish, and the only reasons a review is removed.',
      url: '/review-policy',
    });
  }
}
