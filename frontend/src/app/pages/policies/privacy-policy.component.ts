import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-privacy-policy',
  templateUrl: './privacy-policy.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class PrivacyPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Privacy Policy',
      description: 'Eden Relics privacy policy — how we collect, use, and protect your personal data.',
      url: '/privacy-policy',
    });
  }
}
