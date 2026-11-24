import { TestBed } from '@angular/core/testing';
import { EarlyAccessService } from './early-access.service';

describe('EarlyAccessService', () => {
  const token = `${'a'.repeat(32)}.1893456000.${'b'.repeat(43)}`;

  afterEach(() => {
    localStorage.removeItem('eden_ea');
    window.history.replaceState(null, '', '/');
  });

  it('keeps the token from the emailed link and takes it out of the address bar', () => {
    window.history.replaceState(null, '', `/early-access?ea=${token}&utm_source=newsletter`);
    const service = TestBed.inject(EarlyAccessService);
    expect(service.token()).toBe(token);
    expect(service.headers().get('X-Early-Access')).toBe(token);
    expect(localStorage.getItem('eden_ea')).toBe(token);
    expect(window.location.search).toBe('?utm_source=newsletter');
  });

  it('ignores anything that is not a token, and sends no header without one', () => {
    window.history.replaceState(null, '', '/early-access?ea=<script>');
    const service = TestBed.inject(EarlyAccessService);
    expect(service.token()).toBeNull();
    expect(service.headers().has('X-Early-Access')).toBe(false);
    expect(window.location.search).toBe('');
  });
});
