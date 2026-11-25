import { routes } from '../app.routes';
import { normaliseSectionPath, reservedFirstSegments, sectionPathState } from './section-page-path';

describe('section page addresses', () => {
  const reserved = reservedFirstSegments(routes);

  it('reserves the first part of every real route, and the API', () => {
    for (const segment of ['shop', 'product', 'collections', 'top-picks', 'about', 'admin', 'api']) {
      expect(reserved.has(segment)).toBe(true);
    }
    expect(reserved.has('**')).toBe(false);
  });

  it('treats an address the site already serves as existing', () => {
    expect(sectionPathState('/shop/sale', reserved)).toBe('existing');
    expect(sectionPathState('/about', reserved)).toBe('existing');
    expect(sectionPathState('/', reserved)).toBe('existing');
  });

  it('accepts a new, tidy address and tidies case and a trailing slash', () => {
    expect(sectionPathState('/autumn-knits', reserved)).toBe('creatable');
    expect(sectionPathState('/edits/autumn-knits', reserved)).toBe('creatable');
    expect(sectionPathState('/Autumn-Knits/', reserved)).toBe('creatable');
    expect(normaliseSectionPath(' /Autumn-Knits/ ')).toBe('/autumn-knits');
  });

  it('flags addresses that cannot be a page', () => {
    expect(sectionPathState('', reserved)).toBe('empty');
    expect(sectionPathState('autumn', reserved)).toBe('invalid');
    expect(sectionPathState('//evil.example', reserved)).toBe('invalid');
    expect(sectionPathState('/autumn_knits', reserved)).toBe('invalid');
    expect(sectionPathState('/a/b/c/d', reserved)).toBe('invalid');
    expect(sectionPathState('/sitemap.xml', reserved)).toBe('invalid');
  });
});
