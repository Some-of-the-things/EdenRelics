import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-compliance-report',
  templateUrl: './compliance-report.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class ComplianceReportComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Shareholders Compliance Report',
      description: 'Eden Relics shareholders compliance report — regulatory and governance overview.',
      url: '/compliance-report',
    });
  }
}
