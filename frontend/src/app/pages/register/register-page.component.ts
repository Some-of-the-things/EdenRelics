import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { SocialLoginComponent } from '../../components/social-login/social-login.component';

@Component({
  selector: 'app-register-page',
  imports: [FormsModule, RouterLink, SocialLoginComponent],
  templateUrl: './register-page.component.html',
  styleUrl: './register-page.component.scss',
})
export class RegisterPageComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  firstName = '';
  lastName = '';
  email = '';
  password = '';
  readonly error = signal('');

  submit(): void {
    this.auth.register(this.email, this.password, this.firstName, this.lastName).subscribe({
      next: () => this.router.navigate(['/account']),
      error: (err) => {
        this.error.set(err.error?.message ?? 'Registration failed. Please try again.');
      },
    });
  }

  onSocialLogin(event: { provider: string; idToken: string }): void {
    this.auth.externalLogin(event.provider, event.idToken).subscribe({
      next: () => this.router.navigate(['/account']),
      error: () => this.error.set('Social login failed. Please try again.'),
    });
  }
}
