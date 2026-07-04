import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-security-report',
  templateUrl: './security-report.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './security-report.component.scss',
})
export class SecurityReportComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Latest Security Report',
      description: 'Eden Relics latest security report — our commitment to protecting your data.',
      url: '/security',
    });
  }
}
