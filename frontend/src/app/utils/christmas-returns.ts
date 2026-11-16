/**
 * The extended Christmas returns window for website orders. The same dates are printed on
 * Teodora's gift card, so don't change them without telling her. Mirrored on the backend in
 * Services/ChristmasReturns.cs (which decides the wording of the buyer's email).
 */
export const CHRISTMAS_RETURNS_TEXT =
  "Orders placed between 1 November and 24 December can be returned until 23 January 2027, as long as they're unworn and in the condition we sent them.";

/** Last moment an order still qualifies (end of 24 Dec UK time; GMT in December). */
const ORDERED_BY = Date.UTC(2026, 11, 24, 23, 59, 59);
/** Last moment a qualifying order can be sent back (end of 23 Jan UK time). */
const RETURN_BY = Date.UTC(2027, 0, 23, 23, 59, 59);

/** Show the note at checkout only while a new order would still be covered. */
export function christmasReturnsOpenForOrders(now: Date = new Date()): boolean {
  return now.getTime() <= ORDERED_BY;
}

/** Keep the policy wording on the returns page until the extended window has closed. */
export function christmasReturnsInEffect(now: Date = new Date()): boolean {
  return now.getTime() <= RETURN_BY;
}
