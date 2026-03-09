import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-verify-email',
  imports: [FormsModule, RouterLink],
  templateUrl: './verify-email.component.html',
  styleUrl: './verify-email.component.scss',
})
export class VerifyEmailComponent {
  private readonly auth = inject(AuthService);

  email = '';
  tokenValue = '';
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
