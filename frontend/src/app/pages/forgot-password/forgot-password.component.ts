import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly seo = inject(SeoService);

  email = '';
  readonly sent = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Forgot Password', url: '/forgot-password', noIndex: true });
  }

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
