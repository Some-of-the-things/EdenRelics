import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CurrencyPipe, TitleCasePipe } from '@angular/common';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { AuthService } from '../../services/auth.service';
import { ProductService } from '../../services/product.service';

@Component({
  selector: 'app-admin-page',
  imports: [FormsModule, CurrencyPipe, TitleCasePipe],
  templateUrl: './admin-page.component.html',
  styleUrl: './admin-page.component.scss',
})
export class AdminPageComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly productService = inject(ProductService);
  readonly store = inject(ProductStore);
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly imagePreview = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly uploadError = signal('');

  form: Omit<Product, 'id'> = this.emptyForm();

  openForm(): void {
    this.editingId.set(null);
    this.imagePreview.set(null);
    this.uploadError.set('');
    this.form = this.emptyForm();
    this.showForm.set(true);
  }

  edit(product: Product): void {
    this.editingId.set(product.id);
    this.imagePreview.set(null);
    this.uploadError.set('');
    this.form = {
      name: product.name,
      description: product.description,
      price: product.price,
      era: product.era,
      category: product.category,
      size: product.size,
      condition: product.condition,
      imageUrl: product.imageUrl,
      inStock: product.inStock,
    };
    this.showForm.set(true);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploadError.set('');
    this.imagePreview.set(URL.createObjectURL(file));
    this.uploading.set(true);

    this.productService.uploadImage(file).subscribe({
      next: (res) => {
        this.form.imageUrl = res.imageUrl;
        this.uploading.set(false);
      },
      error: (err) => {
        this.uploading.set(false);
        this.imagePreview.set(null);
        this.uploadError.set(err.error?.error ?? 'Upload failed. Please try again.');
      },
    });
  }

  save(): void {
    const id = this.editingId();
    if (id) {
      this.store.updateProduct(id, this.form);
    } else {
      this.store.addProduct(this.form);
    }
    this.closeForm();
  }

  remove(id: string): void {
    if (confirm('Delete this product?')) {
      this.store.removeProduct(id);
    }
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  private emptyForm(): Omit<Product, 'id'> {
    return {
      name: '',
      description: '',
      price: 0,
      era: '',
      category: '70s',
      size: '10',
      condition: 'good',
      imageUrl: '',
      inStock: true,
    };
  }
}
