import { Component, input } from '@angular/core';
import { ProductDetailComponent } from '../../components/product-detail/product-detail.component';

@Component({
  selector: 'app-product-page',
  imports: [ProductDetailComponent],
  templateUrl: './product-page.component.html',
  styleUrl: './product-page.component.scss',
})
export class ProductPageComponent {
  readonly id = input.required<string>();
}
