/**
 * Depop — selectors only.
 *
 * No public API either (brief §2), so the same transport and the same rules as Vinted. Kept in its
 * own module purely so that a Vinted redesign cannot take Depop down with it: that isolation is the
 * reason decision 1 chose one extension with per-platform modules over two extensions, and it only
 * pays off if nothing platform-specific leaks out of these files.
 *
 * UNRESEARCHED, and gated on that — see the note in vinted.js. Nothing here submits.
 */

export const depop = {
  platform: 'Depop',

  research: 'unresearched',

  newListingUrl: 'https://www.depop.com/products/create/',

  isListingPage(url) {
    return /^https:\/\/(www\.)?depop\.com\/products\/create/.test(url);
  },

  signedIn(root) {
    return Boolean(root.querySelector('[data-testid="userMenu"], [href^="/settings"]'));
  },

  fields: [
    {
      key: 'description',
      label: 'Describe your item',
      strategies: [
        { by: 'testid', value: 'description__input' },
        { by: 'name', value: 'description' },
        { by: 'label', value: 'Description' },
      ],
    },
    {
      key: 'price',
      label: 'Price',
      strategies: [
        { by: 'testid', value: 'price__input' },
        { by: 'name', value: 'price' },
        { by: 'label', value: 'Price' },
      ],
    },
    {
      key: 'size',
      label: 'Size',
      strategies: [
        { by: 'testid', value: 'size__input' },
        { by: 'name', value: 'size' },
        { by: 'label', value: 'Size' },
      ],
    },
    {
      key: 'condition',
      label: 'Condition',
      strategies: [
        { by: 'testid', value: 'condition__input' },
        { by: 'name', value: 'condition' },
        { by: 'label', value: 'Condition' },
      ],
    },
    // Depop has no separate title field — the description opens with the name and the hashtags do
    // the discovery work, which is why the adapter builds one blob. `title` is deliberately absent
    // rather than mapped to something approximate; a field we cannot honour is not a field.
  ],

  sales: {
    research: 'unresearched',
    url: null,
  },
};
