import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-terms-conditions',
  templateUrl: './terms-conditions.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class TermsConditionsComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Terms & Conditions',
      description: 'Eden Relics terms and conditions of use, covering website usage, orders, liability, and dispute resolution.',
      url: '/terms-conditions',
    });
  }
}
