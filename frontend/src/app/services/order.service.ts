import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

export interface CreateOrderItem {
  productId: string;
  quantity: number;
}

export interface OrderAddress {
  addressLine1: string;
  addressLine2?: string;
  city: string;
  county?: string;
  postcode: string;
  country: string;
}

export interface CheckoutRequest {
  items: CreateOrderItem[];
  guestEmail?: string | null;
  shippingAddress?: OrderAddress;
  billingAddress?: OrderAddress;
  shippingMethod?: string;
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

export interface CheckoutResponse {
  orderId: string;
  checkoutUrl: string;
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.apiUrl}/api/orders`;

  private get authHeaders(): HttpHeaders {
    const token = this.auth.getToken();
    return token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : new HttpHeaders();
  }

  checkout(request: CheckoutRequest): Observable<CheckoutResponse> {
    return this.http.post<CheckoutResponse>(
      this.apiUrl,
      request,
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
