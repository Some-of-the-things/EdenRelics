import { isMember, keywordMatches, membershipReason, MembershipRule } from './category-membership';
import { slugify } from '../pages/admin/admin-categories.component';
import { BUILT_IN_CATEGORIES } from '../services/category.service';

const rule = (over: Partial<MembershipRule> = {}): MembershipRule => ({
  includeKeywords: ['maxi'],
  excludeKeywords: ['skirt'],
  addedProductIds: [],
  removedProductIds: [],
  ...over,
});

describe('category membership (keywords plus ticks)', () => {
  it('matches on the name, case-insensitively, unless an exclude word is present', () => {
    expect(keywordMatches('1970s Boho MAXI Dress', ['maxi'], [])).toBe(true);
    expect(keywordMatches('1970s Maxi Skirt', ['maxi'], ['skirt'])).toBe(false);
    expect(keywordMatches('1990s Midi Dress', ['maxi'], [])).toBe(false);
    expect(keywordMatches('Anything', [], [])).toBe(false);
  });

  it('a piece added by hand is in even when its name does not match', () => {
    const r = rule({ addedProductIds: ['p1'] });
    expect(isMember({ id: 'p1', name: 'Midi Dress' }, r)).toBe(true);
    expect(membershipReason({ id: 'p1', name: 'Midi Dress' }, r)).toBe('added');
  });

  it('a piece removed by hand is out even when its name matches', () => {
    const r = rule({ removedProductIds: ['p2'] });
    expect(isMember({ id: 'p2', name: 'Maxi Dress' }, r)).toBe(false);
    expect(membershipReason({ id: 'p2', name: 'Maxi Dress' }, r)).toBe('removed');
  });

  it('otherwise follows the keywords', () => {
    expect(membershipReason({ id: 'p3', name: 'Maxi Dress' }, rule())).toBe('keyword');
    expect(membershipReason({ id: 'p4', name: 'Midi Dress' }, rule())).toBe('none');
  });
});

describe('slugify', () => {
  it('makes a URL slug from a name', () => {
    expect(slugify('Whimsigoth')).toBe('whimsigoth');
    expect(slugify('R&K Originals')).toBe('r-k-originals');
    expect(slugify('  Boho & Paisley!  ')).toBe('boho-paisley');
    expect(slugify('Café Society')).toBe('cafe-society');
  });
});

describe('built-in categories (fallback, and the source the database was seeded from)', () => {
  it('has the 15 launch pages under the three hubs with their current URLs', () => {
    const byHub = Object.fromEntries(BUILT_IN_CATEGORIES.hubs.map((h) => [h.key, h.subCategories.map((s) => s.slug)]));
    expect(byHub['designers'].length).toBe(10);
    expect(byHub['style']).toEqual(['cottagecore', 'prairie', 'boho']);
    expect(byHub['dresses']).toEqual(['maxi', 'midi']);
  });
});
