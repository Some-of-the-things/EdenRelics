import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-reset-password',
  imports: [FormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss',
})
export class ResetPasswordComponent {
  private readonly auth = inject(AuthService);

  email = '';
  tokenValue = '';
  newPassword = '';
  confirmPassword = '';
  readonly done = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');

  submit(): void {
    if (this.newPassword !== this.confirmPassword) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.processing.set(true);
    this.error.set('');

    this.auth.resetPassword(this.email, this.tokenValue, this.newPassword).subscribe({
      next: () => {
        this.processing.set(false);
        this.done.set(true);
      },
      error: (err) => {
        this.processing.set(false);
        this.error.set(err.error?.message ?? 'Reset failed. Please try again.');
      },
    });
  }
}
