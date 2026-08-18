/**
 * Vinted — selectors only.
 *
 * Extension-only by necessity: the Pro Integrations API is allowlisted to a handful of Vinted Pro
 * businesses, appears single-tenant, and our application should be assumed refused (brief §2). Every
 * competitor ships an extension for exactly this reason.
 *
 * THIS TABLE IS UNRESEARCHED AND THE EXTENSION KNOWS IT. Brief §6 makes field mapping Teodora's
 * task — the form's real field names, its condition enum and its size taxonomy have to be recorded
 * by listing something by hand. Until that happens `research` stays 'unresearched' and nothing here
 * is allowed to touch a form; the seller gets the paste fallback instead. The selectors below are
 * the shape the table will take, not values anybody has confirmed, and shipping them as "probably
 * right" is precisely the silent-wrong-field failure the gate exists to prevent.
 *
 * Nothing in this file submits. There is no publish selector, by design — see registry.js.
 */

export const vinted = {
  platform: 'Vinted',

  /** Flip to 'documented' only when every field below has been confirmed against the live form. */
  research: 'unresearched',

  /** Where a new listing starts. The seller lands here and reviews what we filled. */
  newListingUrl: 'https://www.vinted.co.uk/items/new',

  /** Whether a given page is the one we can work on. */
  isListingPage(url) {
    return /^https:\/\/(www\.)?vinted\.co\.uk\/items\/new/.test(url);
  },

  /**
   * A cheap signed-in check. We never hold Vinted credentials (§9.5 — settled: we never will), so a
   * signed-out seller is something only they can fix, and saying so beats a selector-miss report
   * that blames the DOM for a session problem.
   */
  signedIn(root) {
    return Boolean(
      root.querySelector('[data-testid="header-user-menu"], [data-testid="user-menu-button"]'),
    );
  },

  /**
   * Fields, in the order a person would fill them.
   *
   * Order matters beyond tidiness: platform forms reveal later fields based on earlier ones (pick a
   * category, get a size list), so filling top-down is both the human pattern and the only order
   * that works.
   */
  fields: [
    {
      key: 'title',
      label: 'Title',
      strategies: [
        { by: 'testid', value: 'item-title-input' },
        { by: 'name', value: 'title' },
        { by: 'label', value: 'Title' },
      ],
    },
    {
      key: 'description',
      label: 'Describe your item',
      strategies: [
        { by: 'testid', value: 'item-description-input' },
        { by: 'name', value: 'description' },
        { by: 'label', value: 'Describe your item' },
      ],
    },
    {
      key: 'price',
      label: 'Price',
      strategies: [
        { by: 'testid', value: 'item-price-input' },
        { by: 'name', value: 'price' },
        { by: 'label', value: 'Price' },
      ],
    },
    // Size and condition are dropdowns backed by Vinted's own taxonomies. They are listed so the
    // table is complete, but they are the two that most need Teodora's enum: our free-text "Size 12"
    // and "Very good" have to become whatever Vinted's list actually offers, and a near-miss on
    // condition is a listing that misdescribes the garment.
    {
      key: 'size',
      label: 'Size',
      strategies: [
        { by: 'testid', value: 'item-size-input' },
        { by: 'name', value: 'size_id' },
        { by: 'label', value: 'Size' },
      ],
    },
    {
      key: 'condition',
      label: 'Condition',
      strategies: [
        { by: 'testid', value: 'item-status-input' },
        { by: 'name', value: 'status_id' },
        { by: 'label', value: 'Condition' },
      ],
    },
  ],

  /**
   * Sale detection while the browser happens to be open (decision 2, 14 Aug 2026).
   *
   * There is no server-side route, so this is the honest best we can do, and the honesty is the
   * point: a Vinted sale may not propagate until the seller next opens Vinted, which is told to them
   * at onboarding rather than discovered at 3am. The mechanism is unresearched for the same reason
   * as the form — guessing at which request lists sold items would be guessing about money.
   */
  sales: {
    research: 'unresearched',
    url: null,
  },
};
