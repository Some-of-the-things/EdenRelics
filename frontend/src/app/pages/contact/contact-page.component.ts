import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-contact-page',
  imports: [FormsModule],
  templateUrl: './contact-page.component.html',
  styleUrl: './contact-page.component.scss',
})
export class ContactPageComponent {
  private readonly http = inject(HttpClient);

  name = '';
  email = '';
  subject = '';
  message = '';
  readonly sent = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');

  submit(): void {
    this.processing.set(true);
    this.error.set('');

    this.http
      .post(`${environment.apiUrl}/api/contact`, {
        name: this.name,
        email: this.email,
        subject: this.subject,
        message: this.message,
      })
      .subscribe({
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
