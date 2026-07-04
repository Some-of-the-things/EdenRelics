import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ContentService } from '../../services/content.service';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-contact-page',
  imports: [FormsModule],
  templateUrl: './contact-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
      description:
        'Get in touch with Eden Relics — questions about a piece, sizing, sourcing, or partnership enquiries.',
      url: '/contact',
      hreflang: true,
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'ContactPage',
          '@id': 'https://edenrelics.co.uk/contact#contactpage',
          url: 'https://edenrelics.co.uk/contact',
          name: 'Contact Eden Relics',
          inLanguage: 'en-GB',
        },
        {
          '@type': 'Organization',
          '@id': 'https://edenrelics.co.uk/#organization',
          name: 'Eden Relics',
          url: 'https://edenrelics.co.uk',
          email: 'edenrelics@dcp-net.com',
          telephone: '+44 7454 905173',
          contactPoint: {
            '@type': 'ContactPoint',
            contactType: 'customer support',
            email: 'edenrelics@dcp-net.com',
            telephone: '+44 7454 905173',
            availableLanguage: ['English'],
          },
          address: {
            '@type': 'PostalAddress',
            streetAddress: '30 Vane Close',
            addressLocality: 'Norwich',
            postalCode: 'NR7 0US',
            addressCountry: 'GB',
          },
        },
        {
          '@type': 'BreadcrumbList',
          itemListElement: [
            { '@type': 'ListItem', position: 1, name: 'Home', item: 'https://edenrelics.co.uk' },
            {
              '@type': 'ListItem',
              position: 2,
              name: 'Contact',
              item: 'https://edenrelics.co.uk/contact',
            },
          ],
        },
      ],
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
