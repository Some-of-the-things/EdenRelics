import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';
import { CHRISTMAS_RETURNS_TEXT, christmasReturnsInEffect } from '../../utils/christmas-returns';

@Component({
  selector: 'app-returns-policy',
  templateUrl: './returns-policy.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './privacy-policy.component.scss',
})
export class ReturnsPolicyComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  // Rendered from code rather than the CMS row, so it shows even though the live policy text
  // comes from the database. It drops off by itself once the extended window has closed.
  readonly christmasReturnsText = CHRISTMAS_RETURNS_TEXT;
  readonly showChristmasReturns = christmasReturnsInEffect();

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Returns Policy',
      description:
        'Eden Relics returns policy — how to return or exchange vintage items purchased from our shop.',
      url: '/returns-policy',
    });
  }
}
