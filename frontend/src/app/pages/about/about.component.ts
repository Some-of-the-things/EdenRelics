import { Component, effect, inject, ChangeDetectionStrategy } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';

const DEFAULT_TITLE = 'Lovingly handpicked vintage';
const DEFAULT_META_TITLE = 'About Eden Relics — Lovingly Handpicked Vintage';
const DEFAULT_META_DESCRIPTION =
  'Eden Relics is a curated vintage shop in Norwich, UK, specialising in 1950s, 60s, 70s, 80s, and 90s dresses — personally sourced, photographed, and chosen for their quality and character.';
const DEFAULT_SIGNATURE = 'Teodora Carter & Peter Carter';
const DEFAULT_CONTENT = `<p>Eden Relics is a curated vintage shop based in Norwich, UK, specialising in dresses from the 1950s, 60s, 70s, 80s, and 90s — the kind that were made to last, cut with intention, and worn by someone who loved them first.</p>
<p>Every piece is personally sourced and chosen for its quality, character, and the way it moves. Wherever possible the dresses are modelled — by one of us or a friend — so you can see how they actually fall on a real body. For pieces that don't suit a modelled shot, we photograph them carefully on a mannequin so nothing is left to guesswork.</p>
<p>We started Eden Relics because we believe in buying less and buying better. Fast fashion has a cost the price tag doesn't show — in waste, in craft, in the stories we throw away. Vintage is the alternative: beautiful things that already exist, waiting to be worn again.</p>
<p>Every purchase here is an act of intention. We hope you find something that feels like it was always meant to be yours.</p>`;
const DEFAULT_FOUNDERS_TITLE = 'Meet the founders';
const DEFAULT_FOUNDERS_CONTENT = `<p>Eden Relics is run by Teodora Carter and Peter Carter, a couple based in Norwich. We started it together to give well-made vintage pieces a second life instead of letting them disappear.</p>
<p>Teodora leads the sourcing, styling, and photography — the eye behind which pieces make the cut. Peter builds and runs the website and looks after the behind-the-scenes side of the shop. Every dress on the site has been chosen, checked, and photographed by the two of us, here in Norwich.</p>
<p>Beyond Eden Relics, Peter also runs <a href="https://dcp-net.com" target="_blank" rel="noopener nofollow">DCP-NET</a>, and together we run <a href="https://food-info.org" target="_blank" rel="noopener nofollow">Food Info</a>.</p>`;

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './about.component.scss',
})
export class AboutComponent {
  private readonly seo = inject(SeoService);
  readonly content = inject(ContentService);

  readonly defaultContent = DEFAULT_CONTENT;
  readonly defaultFoundersTitle = DEFAULT_FOUNDERS_TITLE;
  readonly defaultFoundersContent = DEFAULT_FOUNDERS_CONTENT;

  constructor() {
    // Re-apply SEO tags whenever ContentService updates so admin edits to the
    // meta title / description / about description take effect without a reload.
    effect(() => {
      const metaTitle = this.content.get('page.about.meta.title', DEFAULT_META_TITLE);
      const metaDescription = this.content.get(
        'page.about.meta.description',
        DEFAULT_META_DESCRIPTION,
      );
      const aboutDescription = this.content.get(
        'page.about.jsonld.description',
        'A curated vintage shop based in Norwich, UK, specialising in dresses from the 1950s, 60s, 70s, 80s, and 90s.',
      );

      this.seo.updateTags({
        title: metaTitle,
        description: metaDescription,
        url: '/about',
      });
      this.seo.setJsonLd({
        '@context': 'https://schema.org',
        '@graph': [
          {
            '@type': 'AboutPage',
            name: 'About Eden Relics',
            url: 'https://edenrelics.co.uk/about',
            description: metaDescription,
            mainEntity: { '@id': 'https://edenrelics.co.uk/#organization' },
          },
          {
            // Adds the founder relationship to the canonical Organization node
            // defined on the home page (merged by @id); reference the people by
            // @id so each resolves to a full Person entity below.
            '@type': 'Organization',
            '@id': 'https://edenrelics.co.uk/#organization',
            name: 'Eden Relics',
            url: 'https://edenrelics.co.uk',
            description: aboutDescription,
            founder: [
              { '@id': 'https://edenrelics.co.uk/#teodora-carter' },
              { '@id': 'https://edenrelics.co.uk/#peter-carter' },
            ],
          },
          {
            '@type': 'Person',
            '@id': 'https://edenrelics.co.uk/#teodora-carter',
            name: 'Teodora Carter',
            jobTitle: 'Co-founder',
            worksFor: { '@id': 'https://edenrelics.co.uk/#organization' },
            workLocation: { '@type': 'Place', name: 'Norwich, UK' },
          },
          {
            '@type': 'Person',
            '@id': 'https://edenrelics.co.uk/#peter-carter',
            name: 'Peter Carter',
            jobTitle: 'Co-founder',
            worksFor: { '@id': 'https://edenrelics.co.uk/#organization' },
            workLocation: { '@type': 'Place', name: 'Norwich, UK' },
            sameAs: ['https://www.linkedin.com/in/peterdcarter/'],
          },
        ],
      });
    });
  }
}
