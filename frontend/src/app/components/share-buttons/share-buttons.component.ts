import { ChangeDetectionStrategy, Component, afterNextRender, input, signal } from '@angular/core';

@Component({
  selector: 'app-share-buttons',
  templateUrl: './share-buttons.component.html',
  styleUrl: './share-buttons.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShareButtonsComponent {
  readonly url = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input<string>('');
  readonly image = input<string>('');

  readonly canNativeShare = signal(false);
  readonly copied = signal(false);

  private copyTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // Defer to post-hydration so SSR and initial-client render produce the
    // same HTML (no native-share button) before we add the browser-only one.
    afterNextRender(() => {
      if (typeof navigator !== 'undefined' && 'share' in navigator) {
        this.canNativeShare.set(true);
      }
    });
  }

  pinterestUrl(): string {
    const params = new URLSearchParams({
      url: this.url(),
      media: this.image(),
      description: this.shareSubject(),
    });
    return `https://www.pinterest.com/pin/create/button/?${params.toString()}`;
  }

  facebookUrl(): string {
    return `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(this.url())}`;
  }

  whatsappUrl(): string {
    const text = this.shareSubject() + ' ' + this.url();
    return `https://wa.me/?text=${encodeURIComponent(text)}`;
  }

  emailUrl(): string {
    const subject = encodeURIComponent(this.shareSubject());
    const prefix = this.description() ? this.description() + '\n\n' : '';
    const body = encodeURIComponent(prefix + this.url());
    return `mailto:?subject=${subject}&body=${body}`;
  }

  async copyLink(): Promise<void> {
    if (typeof navigator === 'undefined' || !navigator.clipboard) {
      return;
    }
    try {
      await navigator.clipboard.writeText(this.url());
      this.copied.set(true);
      if (this.copyTimer) {
        clearTimeout(this.copyTimer);
      }
      this.copyTimer = setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // Clipboard permission denied — silently ignore.
    }
  }

  async nativeShare(): Promise<void> {
    if (typeof navigator === 'undefined' || !('share' in navigator)) {
      return;
    }
    try {
      await navigator.share({
        title: this.title(),
        text: this.description() || this.title(),
        url: this.url(),
      });
    } catch {
      // User cancelled or share API errored — silent.
    }
  }

  private shareSubject(): string {
    return this.title();
  }
}
