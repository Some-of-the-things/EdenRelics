import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface OffsiteSale {
  id: string;
  dressName: string;
  era: string;
  category: string;
  size: string;
  condition: string;
  salePrice: number;
  costPrice: number;
  platform: string;
  saleDateUtc: string;
  notes: string | null;
}

export interface CreateOffsiteSale {
  dressName: string;
  era: string;
  category: string;
  size: string;
  condition: string;
  salePrice: number;
  costPrice: number;
  platform: string;
  saleDateUtc: string;
  notes: string | null;
}

@Injectable({ providedIn: 'root' })
export class OffsiteSaleService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/offsitesales`;

  getAll(): Observable<OffsiteSale[]> {
    return this.http.get<OffsiteSale[]>(this.apiUrl);
  }

  create(sale: CreateOffsiteSale): Observable<OffsiteSale> {
    return this.http.post<OffsiteSale>(this.apiUrl, sale);
  }

  update(id: string, sale: CreateOffsiteSale): Observable<OffsiteSale> {
    return this.http.put<OffsiteSale>(`${this.apiUrl}/${id}`, sale);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
