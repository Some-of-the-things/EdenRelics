/**
 * Classic-script loader for the Eden Relics page bridge.
 *
 * Content scripts declared in a manifest cannot be ES modules, and everything real in this extension
 * is one — shared with the service worker and with the Node tests. So the declared script is this
 * three-line shim and the actual code is dynamically imported, which keeps a single copy of the
 * protocol rather than a content-script fork of it that drifts.
 */
(async () => {
  const module = await import(chrome.runtime.getURL('src/content/bridge.js'));
  module.start();
})();
