import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  private readonly auth = inject(AuthService);

  email = '';
  readonly sent = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');

  submit(): void {
    this.processing.set(true);
    this.error.set('');

    this.auth.forgotPassword(this.email).subscribe({
      next: () => {
        this.processing.set(false);
        this.sent.set(true);
      },
      error: () => {
        this.processing.set(false);
        this.error.set('Something went wrong. Please try again.');
      },
    });
  }
}
