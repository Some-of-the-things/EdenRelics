import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-modern-slavery-policy',
  templateUrl: './modern-slavery-policy.component.html',
  styleUrl: './modern-slavery-policy.component.scss',
})
export class ModernSlaveryPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Modern Slavery Policy',
      description: 'Eden Relics modern slavery statement and commitment to ethical practices.',
      url: '/modern-slavery-policy',
    });
  }
}
