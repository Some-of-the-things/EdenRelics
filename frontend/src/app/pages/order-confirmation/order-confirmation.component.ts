import { Component, inject, input, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { OrderService, OrderDto } from '../../services/order.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-order-confirmation',
  imports: [RouterLink, CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './order-confirmation.component.html',
  styleUrl: './order-confirmation.component.scss',
})
export class OrderConfirmationComponent implements OnInit {
  readonly id = input.required<string>();
  private readonly orderService = inject(OrderService);
  private readonly http = inject(HttpClient);
  readonly order = signal<OrderDto | null>(null);
  readonly error = signal('');

  mailingEmail = '';
  readonly mailingSubscribed = signal(false);

  ngOnInit(): void {
    this.orderService.getById(this.id()).subscribe({
      next: (order) => this.order.set(order),
      error: () => this.error.set('Could not load order details. Please check the link and try again.'),
    });
  }

  subscribeToMailingList(): void {
    if (!this.mailingEmail.trim()) return;
    this.http.post(`${environment.apiUrl}/api/mailing-list/subscribe`, {
      email: this.mailingEmail,
      source: 'Order Confirmation',
    }).subscribe({
      next: () => this.mailingSubscribed.set(true),
    });
  }
}
