import { Component, inject, input, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { OrderService, OrderDto } from '../../services/order.service';

@Component({
  selector: 'app-order-confirmation',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './order-confirmation.component.html',
  styleUrl: './order-confirmation.component.scss',
})
export class OrderConfirmationComponent implements OnInit {
  readonly id = input.required<string>();
  private readonly orderService = inject(OrderService);
  readonly order = signal<OrderDto | null>(null);

  ngOnInit(): void {
    this.orderService.getById(this.id()).subscribe((order) => {
      this.order.set(order);
    });
  }
}
