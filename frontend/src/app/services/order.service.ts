import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';

export interface CreateOrderItem {
  productId: string;
  quantity: number;
}

export interface OrderItemDto {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
}

export interface OrderDto {
  id: string;
  status: string;
  total: number;
  createdAtUtc: string;
  items: OrderItemDto[];
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = 'http://localhost:5260/api/orders';

  private get authHeaders(): HttpHeaders {
    const token = this.auth.getToken();
    return token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : new HttpHeaders();
  }

  checkout(items: CreateOrderItem[], guestEmail?: string): Observable<OrderDto> {
    return this.http.post<OrderDto>(
      this.apiUrl,
      { items, guestEmail: guestEmail ?? null },
      { headers: this.authHeaders }
    );
  }

  getMyOrders(): Observable<OrderDto[]> {
    return this.http.get<OrderDto[]>(this.apiUrl, { headers: this.authHeaders });
  }

  getById(id: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${this.apiUrl}/${id}`);
  }
}
