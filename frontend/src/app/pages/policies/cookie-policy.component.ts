import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-cookie-policy',
  templateUrl: './cookie-policy.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class CookiePolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Cookie Policy',
      description: 'Eden Relics cookie policy — how we use cookies and how you can manage your preferences.',
      url: '/cookie-policy',
    });
  }
}
