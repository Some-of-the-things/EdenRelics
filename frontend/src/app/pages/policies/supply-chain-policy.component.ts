import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-supply-chain-policy',
  templateUrl: './supply-chain-policy.component.html',
  styleUrl: './supply-chain-policy.component.scss',
})
export class SupplyChainPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Supply Chain Policy',
      description: 'Eden Relics supply chain transparency and ethical sourcing practices.',
      url: '/supply-chain-policy',
    });
  }
}
