import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CurrencyPipe, DatePipe, TitleCasePipe } from '@angular/common';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { AuthService } from '../../services/auth.service';
import { ProductService } from '../../services/product.service';
import {
  OrderAdminService,
  AdminOrder,
} from '../../services/order-admin.service';

@Component({
  selector: 'app-admin-page',
  imports: [FormsModule, CurrencyPipe, TitleCasePipe, DatePipe],
  templateUrl: './admin-page.component.html',
  styleUrl: './admin-page.component.scss',
})
export class AdminPageComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly productService = inject(ProductService);
  private readonly orderService = inject(OrderAdminService);
  readonly store = inject(ProductStore);

  readonly activeTab = signal<'products' | 'orders'>('products');
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly imagePreview = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly uploadError = signal('');

  // Orders
  readonly orders = signal<AdminOrder[]>([]);
  readonly ordersLoading = signal(false);
  readonly ordersError = signal('');
  readonly statusFilter = signal<string>('all');

  readonly statuses = ['Pending', 'Paid', 'Processing', 'Shipped', 'Delivered', 'Cancelled'];

  form: Omit<Product, 'id'> = this.emptyForm();

  switchTab(tab: 'products' | 'orders'): void {
    this.activeTab.set(tab);
    if (tab === 'orders' && this.orders().length === 0) {
      this.loadOrders();
    }
  }

  loadOrders(): void {
    this.ordersLoading.set(true);
    this.ordersError.set('');
    this.orderService.getAll().subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.ordersLoading.set(false);
      },
      error: () => {
        this.ordersError.set('Failed to load orders.');
        this.ordersLoading.set(false);
      },
    });
  }

  get filteredOrders(): AdminOrder[] {
    const filter = this.statusFilter();
    if (filter === 'all') return this.orders();
    return this.orders().filter((o) => o.status === filter);
  }

  updateOrderStatus(order: AdminOrder, status: string): void {
    this.orderService.updateStatus(order.id, status).subscribe({
      next: (updated) => {
        this.orders.update((orders) =>
          orders.map((o) => (o.id === updated.id ? updated : o))
        );
      },
      error: () => this.ordersError.set('Failed to update order status.'),
    });
  }

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
        this.uploadError.set(
          err.error?.error ?? 'Upload failed. Please try again.'
        );
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
