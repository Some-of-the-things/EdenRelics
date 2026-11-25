import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type HomeSectionKind = 'top-picks' | 'latest-collection' | 'custom';

/** A homepage product strip, as arranged on the admin Home Sections tab. */
export interface HomeSection {
  kind: HomeSectionKind;
  eyebrow: string;
  title: string;
  description: string;
  /** No button when empty. */
  buttonText: string;
  /** Site-relative path, e.g. "/shop/sale". */
  buttonLink: string;
  /** Ignored for Top Picks, which follows its own switch. */
  visible: boolean;
  /** Hand-picked pieces, in order. Custom sections only. */
  productIds: string[];
  /**
   * Custom sections only: the site serves a page at buttonLink showing these pieces, unless that
   * address is already one of the site's own pages (then the button just links to it).
   */
  createPage?: boolean;
}

/** A page created from a custom homepage section, served at its button link. */
export interface SectionPage {
  path: string;
  eyebrow: string;
  title: string;
  description: string;
  productIds: string[];
}

/**
 * The built-in strips (latest collection, then Top Picks). Shown until the API answers, and kept
 * if it can't be reached, so the homepage never loses its strips because of a failed request. Mirrors HomeSectionsService.Defaults.
 */
export const DEFAULT_HOME_SECTIONS: readonly HomeSection[] = [
  {
    kind: 'latest-collection',
    eyebrow: 'Our Latest Collection',
    title: 'Noir',
    description:
      'Dark, moody vintage pieces united by dramatic sleeves, from Jaeger and Laura Ashley to Polly Peck and Louis Féraud.',
    buttonText: 'Browse entire collection',
    buttonLink: '/collections/noir',
    visible: true,
    productIds: [],
  },
  {
    kind: 'top-picks',
    eyebrow: 'Our Top Picks',
    title: 'Our Top Picks',
    description: 'A rotating, hand-chosen selection of standout vintage from across the shop.',
    buttonText: 'See all top picks',
    buttonLink: '/top-picks',
    visible: true,
    productIds: [],
  },
];

@Injectable({ providedIn: 'root' })
export class HomeSectionsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/home-sections`;

  private readonly _sections = signal<readonly HomeSection[]>(DEFAULT_HOME_SECTIONS);
  /** Public sections in display order (hidden ones already left out by the API). */
  readonly sections = this._sections.asReadonly();

  load(): void {
    this.http.get<HomeSection[]>(this.url).subscribe({
      next: (sections) => this._sections.set(sections),
      error: () => {},
    });
  }

  /** The page a section created at this address; errors (404) when there isn't one. */
  getPage(path: string): Observable<SectionPage> {
    return this.http.get<SectionPage>(`${this.url}/page`, { params: { path } });
  }

  getAdmin(): Observable<HomeSection[]> {
    return this.http.get<HomeSection[]>(`${this.url}/admin`);
  }

  save(sections: HomeSection[]): Observable<HomeSection[]> {
    return this.http.put<HomeSection[]>(`${this.url}/admin`, { sections });
  }
}
