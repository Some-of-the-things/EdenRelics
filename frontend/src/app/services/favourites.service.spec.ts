import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FavouritesService } from './favourites.service';
import { AuthService } from './auth.service';
import { PLATFORM_ID } from '@angular/core';

describe('FavouritesService', () => {
  let service: FavouritesService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PLATFORM_ID, useValue: 'browser' },
        // load() short-circuits unless authenticated; stub it as logged-in
        { provide: AuthService, useValue: { isAuthenticated: () => true } },
      ],
    });
    service = TestBed.inject(FavouritesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty favourites', () => {
    expect(service.favouriteIds().size).toBe(0);
  });

  it('should load favourites from API', () => {
    service.load();
    const req = httpMock.expectOne(r => r.url.endsWith('/api/favourites'));
    expect(req.request.method).toBe('GET');
    req.flush([
      { productId: 'id-1', notifyOnSale: false },
      { productId: 'id-2', notifyOnSale: true },
    ]);
    expect(service.favouriteIds().size).toBe(2);
    expect(service.isFavourite('id-1')).toBe(true);
    expect(service.isFavourite('id-3')).toBe(false);
  });

  it('should only load once', () => {
    service.load();
    httpMock.expectOne(r => r.url.endsWith('/api/favourites')).flush([]);
    service.load(); // second call should not trigger HTTP
    httpMock.expectNone(r => r.url.endsWith('/api/favourites'));
  });

  it('should toggle on - add favourite optimistically', () => {
    service.toggle('product-1');
    expect(service.isFavourite('product-1')).toBe(true);
    const req = httpMock.expectOne(r => r.url.includes('/api/favourites/product-1'));
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should toggle off - remove favourite optimistically', () => {
    // First add
    service.toggle('product-1');
    httpMock.expectOne(r => r.url.includes('/api/favourites/product-1')).flush(null);

    // Then remove
    service.toggle('product-1');
    expect(service.isFavourite('product-1')).toBe(false);
    const req = httpMock.expectOne(r => r.url.includes('/api/favourites/product-1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should reset state', () => {
    service.toggle('product-1');
    httpMock.expectOne(r => r.url.includes('/api/favourites/product-1')).flush(null);
    service.reset();
    expect(service.favouriteIds().size).toBe(0);
  });
});
