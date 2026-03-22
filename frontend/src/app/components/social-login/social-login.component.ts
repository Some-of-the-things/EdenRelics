import { Component, afterNextRender, output, signal } from '@angular/core';

declare const google: any;
declare const FB: any;
declare const AppleID: any;

@Component({
  selector: 'app-social-login',
  templateUrl: './social-login.component.html',
  styleUrl: './social-login.component.scss',
})
export class SocialLoginComponent {
  readonly tokenReceived = output<{ provider: string; idToken: string }>();
  readonly ready = signal(false);

  private googleLoaded = false;
  private facebookLoaded = false;

  constructor() {
    afterNextRender({
      read: () => {
        this.ready.set(true);
        this.loadGoogleSdk();
        this.loadFacebookSdk();
      },
    });
  }

  loginWithGoogle(): void {
    if (!this.googleLoaded || typeof google === 'undefined') return;
    google.accounts.id.prompt();
  }

  loginWithFacebook(): void {
    if (typeof FB === 'undefined') return;
    FB.login((response: any) => {
      if (response.authResponse?.accessToken) {
        this.tokenReceived.emit({ provider: 'Facebook', idToken: response.authResponse.accessToken });
      }
    }, { scope: 'email,public_profile' });
  }

  loginWithApple(): void {
    if (typeof AppleID === 'undefined') {
      this.loadAppleSdk().then(() => this.doAppleLogin());
      return;
    }
    this.doAppleLogin();
  }

  private doAppleLogin(): void {
    AppleID.auth.init({
      clientId: 'YOUR_APPLE_CLIENT_ID',
      scope: 'name email',
      redirectURI: window.location.origin + '/login',
      usePopup: true,
    });
    AppleID.auth.signIn().then((res: any) => {
      if (res.authorization?.id_token) {
        this.tokenReceived.emit({ provider: 'Apple', idToken: res.authorization.id_token });
      }
    }).catch(() => {});
  }

  private initializeGoogle(): void {
    google.accounts.id.initialize({
      client_id: '795615736938-ep20lsmjf72ahenf1okv2mljrio799df.apps.googleusercontent.com',
      callback: (response: any) => {
        this.tokenReceived.emit({ provider: 'Google', idToken: response.credential });
      },
    });
    const target = document.getElementById('g_id_signin');
    if (target) {
      google.accounts.id.renderButton(target, { type: 'standard', size: 'large' });
    }
  }

  private loadGoogleSdk(): void {
    if (document.getElementById('google-gsi')) {
      if (typeof google !== 'undefined') {
        this.googleLoaded = true;
        this.initializeGoogle();
      }
      return;
    }
    const script = document.createElement('script');
    script.id = 'google-gsi';
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.onload = () => {
      this.googleLoaded = true;
      this.initializeGoogle();
    };
    document.head.appendChild(script);
  }

  private loadFacebookSdk(): void {
    if (document.getElementById('facebook-jssdk')) return;
    const script = document.createElement('script');
    script.id = 'facebook-jssdk';
    script.src = 'https://connect.facebook.net/en_US/sdk.js';
    script.async = true;
    script.onload = () => {
      this.facebookLoaded = true;
      FB.init({
        appId: 'YOUR_FACEBOOK_APP_ID',
        cookie: true,
        xfbml: false,
        version: 'v19.0',
      });
    };
    document.head.appendChild(script);
  }

  private loadAppleSdk(): Promise<void> {
    return new Promise((resolve) => {
      if (document.getElementById('apple-signin')) { resolve(); return; }
      const script = document.createElement('script');
      script.id = 'apple-signin';
      script.src = 'https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/en_US/appleid.auth.js';
      script.async = true;
      script.onload = () => resolve();
      document.head.appendChild(script);
    });
  }
}
