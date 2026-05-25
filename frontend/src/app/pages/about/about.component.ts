import { Component, inject, OnInit } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss',
})
export class AboutComponent implements OnInit {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  readonly defaultContent = `<p>Eden Relics is a curated vintage shop based in Norwich, UK, specialising in dresses from the 1970s, 80s, and 90s — the kind that were made to last, cut with intention, and worn by someone who loved them first.</p>
<p>Every piece is personally sourced and chosen for its quality, character, and the way it moves. Wherever possible the dresses are modelled — by me or a friend — so you can see how they actually fall on a real body. For pieces that don't suit a modelled shot, I photograph them carefully on a mannequin so nothing is left to guesswork.</p>
<p>I started Eden Relics because I believe in buying less and buying better. Fast fashion has a cost the price tag doesn't show — in waste, in craft, in the stories we throw away. Vintage is the alternative: beautiful things that already exist, waiting to be worn again.</p>
<p>Every purchase here is an act of intention. I hope you find something that feels like it was always meant to be yours.</p>`;

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'About Eden Relics — Lovingly Handpicked Vintage',
      description: 'Eden Relics is a curated vintage shop in Norwich, UK, specialising in 1970s, 80s, and 90s dresses — personally sourced, photographed, and chosen for their quality and character.',
      url: '/about',
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'AboutPage',
      name: 'About Eden Relics',
      url: 'https://edenrelics.co.uk/about',
      mainEntity: {
        '@type': 'Organization',
        name: 'Eden Relics',
        url: 'https://edenrelics.co.uk',
        founder: { '@type': 'Person', name: 'Teodora' },
        description: 'A curated vintage shop based in Norwich, UK, specialising in dresses from the 1970s, 80s, and 90s.',
      },
    });
  }
}
