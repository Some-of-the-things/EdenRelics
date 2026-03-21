import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ProductListComponent } from './product-list.component';

describe('ProductListComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(ProductListComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should have product store injected', () => {
    const fixture = TestBed.createComponent(ProductListComponent);
    expect(fixture.componentInstance.productStore).toBeTruthy();
  });

  it('should have isFavourite method', () => {
    const fixture = TestBed.createComponent(ProductListComponent);
    expect(fixture.componentInstance.isFavourite('some-id')).toBe(false);
  });
});
