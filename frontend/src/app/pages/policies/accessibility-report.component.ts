import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-accessibility-report',
  templateUrl: './accessibility-report.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class AccessibilityReportComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Accessibility Report',
      description: 'Eden Relics WCAG 2.1 Level AA accessibility audit report.',
      url: '/accessibility-report',
    });
  }
}
