import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { PasskeyService } from '../../services/passkey.service';
import { SocialLoginComponent } from '../../components/social-login/social-login.component';

@Component({
  selector: 'app-admin-login',
  imports: [FormsModule, RouterLink, SocialLoginComponent],
  templateUrl: './admin-login.component.html',
  styleUrl: './admin-login.component.scss',
})
export class AdminLoginComponent {
  private readonly auth = inject(AuthService);
  private readonly passkey = inject(PasskeyService);
  private readonly router = inject(Router);
  readonly passkeySupported = signal(false);

  constructor() {
    this.passkeySupported.set(this.passkey.supportsPasskeys());
  }

  email = '';
  password = '';
  mfaCode = '';
  readonly mfaToken = signal('');
  readonly error = signal('');

  submit(): void {
    if (this.mfaToken()) {
      this.auth.mfaVerify(this.mfaToken(), this.mfaCode).subscribe({
        next: () => this.router.navigate(['/account']),
        error: (err) => this.error.set(err.error?.message ?? 'Invalid code.'),
      });
      return;
    }

    this.error.set('');
    this.auth.login(this.email, this.password).subscribe({
      next: (res) => {
        if ('mfaRequired' in res) {
          this.mfaToken.set(res.mfaToken);
          this.error.set('');
        } else {
          this.router.navigate(['/account']);
        }
      },
      error: () => this.error.set('Invalid email or password.'),
    });
  }

  onSocialLogin(event: { provider: string; idToken: string }): void {
    this.auth.externalLogin(event.provider, event.idToken).subscribe({
      next: () => this.router.navigate(['/account']),
      error: () => this.error.set('Social login failed. Please try again.'),
    });
  }

  loginWithPasskey(): void {
    this.error.set('');
    this.passkey.loginWithPasskey(this.email || undefined).subscribe({
      next: () => this.router.navigate(['/account']),
      error: () => this.error.set('Passkey login failed. Please try again.'),
    });
  }
}
