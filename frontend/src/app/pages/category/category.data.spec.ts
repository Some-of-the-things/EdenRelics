import { Product } from '../../models/product.model';
import { CATEGORY_HUBS, CategoryHub, matchProductsToHub } from './category.data';

function makeProduct(name: string, slug: string): Product {
  return {
    id: slug,
    name,
    slug,
    description: 'desc',
    price: 100,
    era: '1970s',
    category: '70s',
    size: '10',
    condition: 'good',
    imageUrl: 'img.jpg',
    inStock: true,
    status: 'live',
  } as Product;
}

const hub: CategoryHub = {
  kind: 'style',
  slug: 'test-hub',
  name: 'Test Hub',
  metaTitle: 'Test',
  metaDescription: 'Test',
  tagline: 'Test',
  intro: 'Test',
  body: [],
  lookFor: [],
  include: ['prairie'],
  exclude: ['reproduction'],
};

describe('matchProductsToHub', () => {
  it('matches on an include keyword in the name', () => {
    const products = [
      makeProduct('1970s Prairie Dress', 'prairie-dress'),
      makeProduct('1980s Denim Jacket', 'denim-jacket'),
    ];
    expect(matchProductsToHub(products, hub).map((p) => p.slug)).toEqual(['prairie-dress']);
  });

  it('lets an exclude keyword veto an include hit', () => {
    const products = [makeProduct('Prairie Dress Reproduction', 'prairie-repro')];
    expect(matchProductsToHub(products, hub)).toEqual([]);
  });

  it('pulls in a curated slug whose name carries no keyword', () => {
    // The reason curatedSlugs exists: a prairie dress listed as a floral maxi.
    const products = [makeProduct('1970s Floral Cotton Maxi Dress', 'floral-cotton-maxi')];
    const curated = { ...hub, curatedSlugs: ['floral-cotton-maxi'] };
    expect(matchProductsToHub(products, curated).map((p) => p.slug)).toEqual(['floral-cotton-maxi']);
  });

  it('lets a curated slug override the exclude list', () => {
    const products = [makeProduct('Prairie Dress Reproduction', 'prairie-repro')];
    const curated = { ...hub, curatedSlugs: ['prairie-repro'] };
    expect(matchProductsToHub(products, curated).map((p) => p.slug)).toEqual(['prairie-repro']);
  });

  it('ignores curated slugs on other hubs', () => {
    const products = [makeProduct('1970s Floral Cotton Maxi Dress', 'floral-cotton-maxi')];
    expect(matchProductsToHub(products, hub)).toEqual([]);
  });
});

describe('CATEGORY_HUBS curation', () => {
  it('has no duplicate slugs within one hub, and no slug curated into a hub twice', () => {
    for (const h of CATEGORY_HUBS) {
      const curated = h.curatedSlugs ?? [];
      expect(new Set(curated).size).toBe(curated.length);
    }
  });
});
