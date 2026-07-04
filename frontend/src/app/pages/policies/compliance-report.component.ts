import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-compliance-report',
  templateUrl: './compliance-report.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './privacy-policy.component.scss',
})
export class ComplianceReportComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Compliance Report',
      description: 'Eden Relics compliance report — regulatory and governance overview.',
      url: '/compliance-report',
    });
  }
}
