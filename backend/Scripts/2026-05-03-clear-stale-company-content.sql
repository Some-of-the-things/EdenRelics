-- One-off cleanup: drop SiteContent rows whose default values were changed
-- when Eden Relics moved from being a trading name of DCPNET LTD to its own
-- registered company (EDEN RELICS LTD, Companies House 17153907).
--
-- After this runs, GetAll will not find rows for these keys, so the API will
-- fall back to the new defaults defined in ContentController.cs.
--
-- Safe to re-run; deleting non-existent rows is a no-op.
-- Wrapped in a transaction so it's atomic.
--
-- To run on production (fly.io postgres):
--   fly postgres connect -a <postgres-app-name>
--   \i 2026-05-03-clear-stale-company-content.sql
--
-- Or pipe it in:
--   cat backend/Scripts/2026-05-03-clear-stale-company-content.sql \
--     | fly postgres connect -a <postgres-app-name>

BEGIN;

DELETE FROM "SiteContent"
WHERE "Key" IN (
  -- Footer company info (registered name, number, address)
  'footer.company.line1',
  'footer.company.line2',
  'footer.company.line3',
  'footer.contact.address',
  -- Policy bodies that referenced DCPNET LTD or VAT
  'policy.privacy.content',
  'policy.returns.content',
  'policy.supply-chain.content',
  'policy.modern-slavery.content',
  'policy.terms.content',
  'policy.cookies.content',
  -- Compliance / report bodies that referenced DCPNET LTD or VAT
  'report.security.content',
  'report.accessibility.content',
  'report.compliance.content'
);

-- Sanity-check: this should return zero rows.
SELECT "Key" FROM "SiteContent"
WHERE "Key" IN (
  'footer.company.line1',
  'footer.company.line2',
  'footer.company.line3',
  'footer.contact.address',
  'policy.privacy.content',
  'policy.returns.content',
  'policy.supply-chain.content',
  'policy.modern-slavery.content',
  'policy.terms.content',
  'policy.cookies.content',
  'report.security.content',
  'report.accessibility.content',
  'report.compliance.content'
);

COMMIT;
