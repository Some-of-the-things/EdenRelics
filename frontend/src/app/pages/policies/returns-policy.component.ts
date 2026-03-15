import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-returns-policy',
  templateUrl: './returns-policy.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class ReturnsPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Returns Policy',
      description: 'Eden Relics returns policy — how to return or exchange vintage items purchased from our shop.',
      url: '/returns-policy',
    });
  }
}
