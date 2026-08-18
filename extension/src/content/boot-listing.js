/** Classic-script loader for the marketplace side. See boot-bridge.js for why this shim exists. */
(async () => {
  const module = await import(chrome.runtime.getURL('src/content/listing.js'));
  module.start();
})();
