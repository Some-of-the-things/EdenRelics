import { Component, inject, OnInit } from '@angular/core';
import { CartComponent } from '../../components/cart/cart.component';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-cart-page',
  imports: [CartComponent],
  templateUrl: './cart-page.component.html',
  styleUrl: './cart-page.component.scss',
})
export class CartPageComponent implements OnInit {
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Shopping Cart',
      description: 'Your Eden Relics shopping cart.',
      url: '/cart',
    });
  }
}
