import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { PasskeyService } from '../../services/passkey.service';
import { SocialLoginComponent } from '../../components/social-login/social-login.component';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-admin-login',
  imports: [FormsModule, RouterLink, SocialLoginComponent],
  templateUrl: './admin-login.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './admin-login.component.scss',
})
export class AdminLoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly passkey = inject(PasskeyService);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  readonly passkeySupported = signal(false);

  constructor() {
    this.passkeySupported.set(this.passkey.supportsPasskeys());
  }

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Sign In', url: '/login', noIndex: true });
  }

  email = '';
  password = '';
  mfaCode = '';
  readonly mfaToken = signal('');
  readonly error = signal('');

  submit(): void {
    if (this.mfaToken()) {
      this.auth.mfaVerify(this.mfaToken(), this.mfaCode).subscribe({
        next: () => this.navigateAfterLogin(),
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
          this.navigateAfterLogin();
        }
      },
      error: () => this.error.set('Invalid email or password.'),
    });
  }

  onSocialLogin(event: { provider: string; idToken: string }): void {
    this.auth.externalLogin(event.provider, event.idToken).subscribe({
      next: () => this.navigateAfterLogin(),
      error: () => this.error.set('Social login failed. Please try again.'),
    });
  }

  loginWithPasskey(): void {
    this.error.set('');
    this.passkey.loginWithPasskey(this.email || undefined).subscribe({
      next: (res) => {
        if ('mfaRequired' in res) {
          this.mfaToken.set(res.mfaToken);
          this.error.set('');
        } else {
          this.navigateAfterLogin();
        }
      },
      error: () => this.error.set('Passkey login failed. Please try again.'),
    });
  }

  private navigateAfterLogin(): void {
    this.router.navigate([this.auth.isAdmin() ? '/admin' : '/account']);
  }
}
