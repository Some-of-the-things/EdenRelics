import { christmasReturnsInEffect, christmasReturnsOpenForOrders } from './christmas-returns';

describe('christmas returns window', () => {
  it('shows the checkout note up to the end of 24 December', () => {
    expect(christmasReturnsOpenForOrders(new Date('2026-11-16T10:00:00Z'))).toBe(true);
    expect(christmasReturnsOpenForOrders(new Date('2026-12-24T23:00:00Z'))).toBe(true);
    expect(christmasReturnsOpenForOrders(new Date('2026-12-25T00:00:01Z'))).toBe(false);
  });

  it('keeps the returns-page wording until the end of 23 January 2027', () => {
    expect(christmasReturnsInEffect(new Date('2027-01-23T22:00:00Z'))).toBe(true);
    expect(christmasReturnsInEffect(new Date('2027-01-24T00:00:01Z'))).toBe(false);
  });
});
