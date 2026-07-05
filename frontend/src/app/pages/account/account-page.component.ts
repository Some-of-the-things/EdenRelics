import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { AccountProfileDto, AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';
import { SeoService } from '../../services/seo.service';
import { EligibleOrder, MyReview, ReviewsService } from '../../services/reviews.service';

@Component({
  selector: 'app-account-page',
  imports: [RouterLink, DatePipe],
  templateUrl: './account-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './account-page.component.scss',
})
export class AccountPageComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly reviews = inject(ReviewsService);

  readonly profile = signal<AccountProfileDto | null>(null);
  readonly isDev = !environment.production;
  readonly resending = signal(false);
  readonly resendDone = signal(false);

  readonly profileError = signal(false);
  readonly eligibleReviews = signal<EligibleOrder[]>([]);
  readonly myReviews = signal<MyReview[]>([]);

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Account', url: '/account', noIndex: true });
    this.auth.getProfile().subscribe({
      next: (p) => this.profile.set(p),
      error: () => this.profileError.set(true),
    });
    this.reviews.getEligible().subscribe({
      next: (o) => this.eligibleReviews.set(o),
      error: () => {},
    });
    this.reviews.getMine().subscribe({
      next: (r) => this.myReviews.set(r),
      error: () => {},
    });
  }

  resendVerification(): void {
    this.resending.set(true);
    this.auth.resendVerification().subscribe({
      next: () => {
        this.resending.set(false);
        this.resendDone.set(true);
      },
      error: () => this.resending.set(false),
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
