import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AccountProfileDto, AuthService } from '../../services/auth.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-account-page',
  imports: [RouterLink],
  templateUrl: './account-page.component.html',
  styleUrl: './account-page.component.scss',
})
export class AccountPageComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly profile = signal<AccountProfileDto | null>(null);
  readonly isDev = !environment.production;
  readonly resending = signal(false);
  readonly resendDone = signal(false);

  ngOnInit(): void {
    this.auth.getProfile().subscribe(p => this.profile.set(p));
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
