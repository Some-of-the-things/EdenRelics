import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ContentService } from '../../services/content.service';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-contact-page',
  imports: [FormsModule],
  templateUrl: './contact-page.component.html',
  styleUrl: './contact-page.component.scss',
})
export class ContactPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  readonly cms = inject(ContentService);

  name = '';
  email = '';
  subject = '';
  message = '';
  readonly sent = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Contact',
      description: 'Get in touch with Eden Relics — questions about a piece, sizing, sourcing, or partnership enquiries.',
      url: '/contact',
      hreflang: true,
    });
  }

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
