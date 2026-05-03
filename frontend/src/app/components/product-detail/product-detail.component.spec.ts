import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ProductDetailComponent } from './product-detail.component';
import { ComponentRef } from '@angular/core';

describe('ProductDetailComponent', () => {
  let componentRef: ComponentRef<ProductDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductDetailComponent);
    componentRef = fixture.componentRef;
    componentRef.setInput('id', 'test-id');
  });

  it('should create', () => {
    expect(componentRef.instance).toBeTruthy();
  });

  it('should have isFavourite method returning false by default', () => {
    expect(componentRef.instance.isFavourite('test-id')).toBe(false);
  });

  it('should return falsy for non-existent product', () => {
    expect(componentRef.instance.product()).toBeFalsy();
  });
});
