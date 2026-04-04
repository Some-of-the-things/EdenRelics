import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-security-report',
  templateUrl: './security-report.component.html',
  styleUrl: './security-report.component.scss',
})
export class SecurityReportComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Security Assessment',
      description: 'Eden Relics security assessment summary — our commitment to protecting your data.',
      url: '/security',
    });
  }
}
