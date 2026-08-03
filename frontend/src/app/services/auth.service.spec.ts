import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

/**
 * Session restore is the thing that decides whether the UI claims you are signed in. Getting it
 * wrong is invisible: a stale session looks completely normal until an API call fails, which is
 * exactly how one survived from 8 July to 3 August unnoticed.
 */
describe('AuthService session restore', () => {
  const DAY = 24 * 60 * 60 * 1000;
  const user = { id: '1', email: 'a@b.c', firstName: 'A', lastName: 'B', role: 'Admin' };

  /** A syntactically real JWT whose payload expires at the given time. Signature is irrelevant. */
  function tokenExpiringAt(when: number): string {
    const payload = btoa(JSON.stringify({ sub: '1', exp: Math.floor(when / 1000) }));
    return `${btoa(JSON.stringify({ alg: 'HS256' }))}.${payload}.sig`;
  }

  function store(token: string | null): void {
    localStorage.setItem('eden_user', JSON.stringify(user));
    if (token !== null) {
      localStorage.setItem('eden_token', token);
    }
  }

  function build(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('restores a session whose token is still valid', () => {
    store(tokenExpiringAt(Date.now() + DAY));
    const service = build();

    expect(service.isAuthenticated()).toBe(true);
    expect(localStorage.getItem('eden_token')).not.toBeNull();
  });

  it('clears a session whose token is past the server renewal window', () => {
    store(tokenExpiringAt(Date.now() - 31 * DAY));
    const service = build();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('eden_token')).toBeNull();
    expect(localStorage.getItem('eden_user')).toBeNull();
  });

  it('clears a session whose token is unreadable', () => {
    store('not-a-jwt');
    const service = build();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('eden_user')).toBeNull();
  });

  it('clears a stored user with no token at all', () => {
    store(null);
    const service = build();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('eden_user')).toBeNull();
  });

  it('renews an expired but still-renewable token instead of signing out', async () => {
    // The case that actually bit: expired weeks ago, inside the 30-day window.
    store(tokenExpiringAt(Date.now() - 26 * DAY));
    const service = build();
    const http = TestBed.inject(HttpTestingController);

    // Restored optimistically — the session is recoverable, so the UI should not flicker out.
    expect(service.isAuthenticated()).toBe(true);

    await new Promise((resolve) => setTimeout(resolve));
    const fresh = tokenExpiringAt(Date.now() + DAY);
    http.expectOne(`${environment.apiUrl}/api/auth/refresh`).flush({ token: fresh, user });

    expect(localStorage.getItem('eden_token')).toBe(fresh);
    expect(service.isAuthenticated()).toBe(true);
    http.verify();
  });

  it('signs out when the server refuses to renew', async () => {
    store(tokenExpiringAt(Date.now() - 26 * DAY));
    const service = build();
    const http = TestBed.inject(HttpTestingController);

    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne(`${environment.apiUrl}/api/auth/refresh`)
      .flush({ message: 'Token too old to refresh.' }, { status: 401, statusText: 'Unauthorized' });

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('eden_token')).toBeNull();
    http.verify();
  });

  it('does not hand out a token the server will never accept', () => {
    store(tokenExpiringAt(Date.now() - 31 * DAY));
    const service = build();

    expect(service.getToken()).toBeNull();
  });

  it('still hands out an expired token inside the window, so it can be renewed', () => {
    const token = tokenExpiringAt(Date.now() - DAY);
    store(token);
    const service = build();

    expect(service.getToken()).toBe(token);
  });
});
