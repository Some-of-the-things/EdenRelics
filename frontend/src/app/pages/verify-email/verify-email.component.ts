import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-verify-email',
  imports: [FormsModule, RouterLink],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.scss',
})
export class VerifyEmailComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  email = '';
  tokenValue = '';

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.email = params.get('email') ?? '';
    this.tokenValue = params.get('token') ?? '';
  }
  readonly done = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');

  submit(): void {
    this.processing.set(true);
    this.error.set('');

    this.auth.verifyEmail(this.email, this.tokenValue).subscribe({
      next: () => {
        this.processing.set(false);
        this.done.set(true);
      },
      error: (err) => {
        this.processing.set(false);
        this.error.set(err.error?.message ?? 'Verification failed. Please try again.');
      },
    });
  }
}
