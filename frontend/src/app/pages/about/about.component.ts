import { Component, effect, inject } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

const DEFAULT_TITLE = 'Lovingly handpicked vintage';
const DEFAULT_META_TITLE = 'About Eden Relics — Lovingly Handpicked Vintage';
const DEFAULT_META_DESCRIPTION = 'Eden Relics is a curated vintage shop in Norwich, UK, specialising in 1970s, 80s, and 90s dresses — personally sourced, photographed, and chosen for their quality and character.';
const DEFAULT_SIGNATURE = 'Teodora, Eden Relics';
const DEFAULT_CONTENT = `<p>Eden Relics is a curated vintage shop based in Norwich, UK, specialising in dresses from the 1970s, 80s, and 90s — the kind that were made to last, cut with intention, and worn by someone who loved them first.</p>
<p>Every piece is personally sourced and chosen for its quality, character, and the way it moves. Wherever possible the dresses are modelled — by me or a friend — so you can see how they actually fall on a real body. For pieces that don't suit a modelled shot, I photograph them carefully on a mannequin so nothing is left to guesswork.</p>
<p>I started Eden Relics because I believe in buying less and buying better. Fast fashion has a cost the price tag doesn't show — in waste, in craft, in the stories we throw away. Vintage is the alternative: beautiful things that already exist, waiting to be worn again.</p>
<p>Every purchase here is an act of intention. I hope you find something that feels like it was always meant to be yours.</p>`;

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  styleUrl: './about.component.scss',
})
export class AboutComponent {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  readonly defaultContent = DEFAULT_CONTENT;

  constructor() {
    // Re-apply SEO tags whenever ContentService updates so admin edits to the
    // meta title / description / about description take effect without a reload.
    effect(() => {
      const metaTitle = this.content.get('page.about.meta.title', DEFAULT_META_TITLE);
      const metaDescription = this.content.get('page.about.meta.description', DEFAULT_META_DESCRIPTION);
      const aboutDescription = this.content.get(
        'page.about.jsonld.description',
        'A curated vintage shop based in Norwich, UK, specialising in dresses from the 1970s, 80s, and 90s.',
      );

      this.seo.updateTags({
        title: metaTitle,
        description: metaDescription,
        url: '/about',
      });
      this.seo.setJsonLd({
        '@context': 'https://schema.org',
        '@type': 'AboutPage',
        name: 'About Eden Relics',
        url: 'https://edenrelics.co.uk/about',
        description: metaDescription,
        mainEntity: {
          '@type': 'Organization',
          name: 'Eden Relics',
          url: 'https://edenrelics.co.uk',
          founder: { '@type': 'Person', name: 'Teodora' },
          description: aboutDescription,
        },
      });
    });
  }
}
