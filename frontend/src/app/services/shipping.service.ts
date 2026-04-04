import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ShippingCountry {
  code: string;
  name: string;
}

export interface ShippingZone {
  zone: string;
  label: string;
  deliveryEstimate: string;
  price: number;
  countries: ShippingCountry[];
}

export interface ShippingRate {
  zone: string;
  label: string;
  deliveryEstimate: string;
  price: number;
  method: string;
}

@Injectable({ providedIn: 'root' })
export class ShippingService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/api/shipping`;

  private zones$?: Observable<ShippingZone[]>;

  getZones(): Observable<ShippingZone[]> {
    if (!this.zones$) {
      this.zones$ = this.http.get<ShippingZone[]>(`${this.apiUrl}/countries`).pipe(
        shareReplay(1)
      );
    }
    return this.zones$;
  }

  getRate(country: string): Observable<ShippingRate> {
    return this.http.get<ShippingRate>(`${this.apiUrl}/rate`, {
      params: { country }
    });
  }
}
