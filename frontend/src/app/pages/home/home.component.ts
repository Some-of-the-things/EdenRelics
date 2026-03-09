import { Component, inject, OnInit } from '@angular/core';
import { ProductListComponent } from '../../components/product-list/product-list.component';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-home',
  imports: [ProductListComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.updateTags({
      url: '/',
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: 'Eden Relics',
      url: 'https://www.edenrelics.co.uk',
      description: 'Curated vintage clothing from decades past.',
    });
  }
}
