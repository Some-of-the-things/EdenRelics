import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AdminOrder {
  id: string;
  status: string;
  total: number;
  createdAtUtc: string;
  customerEmail: string;
  customerName: string | null;
  items: AdminOrderItem[];
}

export interface AdminOrderItem {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
}

@Injectable({ providedIn: 'root' })
export class OrderAdminService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/orders`;

  getAll(): Observable<AdminOrder[]> {
    return this.http.get<AdminOrder[]>(`${this.apiUrl}/admin/all`);
  }

  updateStatus(id: string, status: string): Observable<AdminOrder> {
    return this.http.put<AdminOrder>(`${this.apiUrl}/admin/${id}/status`, { status });
  }
}
