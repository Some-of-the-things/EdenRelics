import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-supply-chain-policy',
  templateUrl: './supply-chain-policy.component.html',
  styleUrl: './privacy-policy.component.scss',
})
export class SupplyChainPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Supply Chain Policy',
      description: 'Eden Relics supply chain transparency and ethical sourcing practices.',
      url: '/supply-chain-policy',
    });
  }
}
