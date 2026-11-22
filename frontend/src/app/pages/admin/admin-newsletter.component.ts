import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe, DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Observable, map, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';
import {
  AdminSubscriber,
  EditorMode,
  Newsletter,
  NewsletterAdminService,
} from '../../services/newsletter-admin.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';

type View = 'compose' | 'history' | 'subscribers';
type SubscriberFilter = 'all' | 'active' | 'unsubscribed';

/** Escapes text for an HTML attribute or element. */
function esc(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function money(value: number): string {
  return `£${value.toFixed(2).replace(/\.00$/, '')}`;
}

/**
 * Email-safe cards (image, title, price) for the chosen pieces, two to a row; the template's
 * mobile rule stacks them on phones.
 */
export function productCardsHtml(products: Pick<Product, 'id' | 'slug' | 'name' | 'price' | 'salePrice' | 'imageUrl'>[], siteUrl: string): string {
  const cell = (p: (typeof products)[number]): string => {
    const href = `${siteUrl}/product/${encodeURIComponent(p.slug || p.id)}`;
    const onSale = p.salePrice != null && p.salePrice < p.price;
    const price = onSale
      ? `${money(p.salePrice!)} <span style="text-decoration:line-through;color:#8a7560;">${money(p.price)}</span>`
      : money(p.price);
    return (
      `<td class="er-card" width="50%" valign="top" style="padding:8px;">` +
      `<a href="${esc(href)}" style="text-decoration:none;color:#2E1A0E;">` +
      `<img src="${esc(p.imageUrl)}" alt="${esc(p.name)}" width="250" style="width:100%;max-width:250px;height:auto;display:block;border:0;">` +
      `<span style="display:block;font-family:Georgia,serif;font-size:16px;line-height:1.35;margin-top:8px;">${esc(p.name)}</span>` +
      `<span style="display:block;font-family:Georgia,serif;font-size:15px;color:#9B4A1E;margin-top:4px;">${price}</span>` +
      `</a></td>`
    );
  };
  const rows: string[] = [];
  for (let i = 0; i < products.length; i += 2) {
    const pair = products.slice(i, i + 2);
    rows.push(`<tr>${pair.map(cell).join('')}${pair.length === 1 ? '<td class="er-card" width="50%" style="padding:8px;"></td>' : ''}</tr>`);
  }
  return `<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 16px;">${rows.join('')}</table>`;
}

@Component({
  selector: 'app-admin-newsletter',
  imports: [DatePipe],
  templateUrl: './admin-newsletter.component.html',
  styleUrl: './admin-newsletter.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminNewsletterComponent implements OnInit {
  private readonly api = inject(NewsletterAdminService);
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly doc = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  readonly store = inject(ProductStore);

  private readonly visualEditor = viewChild<ElementRef<HTMLDivElement>>('visualEditor');

  readonly view = signal<View>('compose');
  readonly audience = signal<number | null>(null);
  readonly newsletters = signal<Newsletter[]>([]);

  // Compose
  readonly editingId = signal<string | null>(null);
  readonly editingStatus = signal<Newsletter['status']>('Draft');
  readonly subject = signal('');
  readonly previewText = signal('');
  readonly bodyHtml = signal('');
  readonly mode = signal<EditorMode>('visual');
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly testEmail = signal('');
  readonly scheduleAt = signal('');
  readonly dirty = signal(false);

  // Preview
  readonly previewHtml = signal<SafeHtml | null>(null);
  readonly previewWidth = signal<'desktop' | 'mobile'>('desktop');

  // Product picker
  readonly pickerOpen = signal(false);
  readonly pickerSearch = signal('');
  readonly picked = signal<ReadonlySet<string>>(new Set());
  readonly pickerResults = computed(() => {
    const term = this.pickerSearch().trim().toLowerCase();
    return this.store
      .liveProducts()
      .filter((p) => !term || p.name.toLowerCase().includes(term) || (p.sku ?? '').toLowerCase().includes(term))
      .slice(0, 60);
  });

  // Subscribers
  readonly subscribers = signal<AdminSubscriber[]>([]);
  readonly subscriberSearch = signal('');
  readonly subscriberFilter = signal<SubscriberFilter>('active');
  readonly subscribersLoading = signal(false);

  readonly locked = computed(() => this.editingStatus() === 'Sending' || this.editingStatus() === 'Sent');
  readonly subjectCount = computed(() => this.subject().length);
  readonly previewTextCount = computed(() => this.previewText().length);
  readonly anySending = computed(() => this.newsletters().some((n) => n.status === 'Sending'));

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.testEmail.set(this.auth.currentUser()?.email ?? '');
    this.store.loadProducts();
    this.refreshAudience();
    this.refreshList();
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  setView(view: View): void {
    this.view.set(view);
    this.message.set(null);
    this.error.set(null);
    if (view === 'history') {
      this.refreshList();
    } else if (view === 'subscribers') {
      this.loadSubscribers();
    } else {
      // The editor element is recreated when the compose view comes back.
      queueMicrotask(() => this.writeVisual(this.bodyHtml()));
    }
  }

  // ---------- Compose ----------

  newNewsletter(): void {
    if (this.dirty() && !confirm('Discard the changes to the current newsletter?')) {
      return;
    }
    this.editingId.set(null);
    this.editingStatus.set('Draft');
    this.subject.set('');
    this.previewText.set('');
    this.bodyHtml.set('');
    this.mode.set('visual');
    this.previewHtml.set(null);
    this.scheduleAt.set('');
    this.dirty.set(false);
    this.view.set('compose');
    this.message.set(null);
    this.error.set(null);
    queueMicrotask(() => this.writeVisual(''));
  }

  open(n: Newsletter): void {
    this.api.get(n.id).subscribe({
      next: (full) => {
        this.editingId.set(full.id);
        this.editingStatus.set(full.status);
        this.subject.set(full.subject);
        this.previewText.set(full.previewText);
        this.bodyHtml.set(full.bodyHtml);
        this.mode.set(full.editorMode);
        this.scheduleAt.set(full.scheduledForUtc ? this.toLocalInput(full.scheduledForUtc) : '');
        this.previewHtml.set(null);
        this.dirty.set(false);
        this.view.set('compose');
        this.message.set(null);
        this.error.set(null);
        queueMicrotask(() => this.writeVisual(full.bodyHtml));
      },
      error: () => this.error.set('Could not open that newsletter.'),
    });
  }

  onSubject(value: string): void {
    this.subject.set(value);
    this.dirty.set(true);
  }

  onPreviewText(value: string): void {
    this.previewText.set(value);
    this.dirty.set(true);
  }

  onHtmlInput(value: string): void {
    this.bodyHtml.set(value);
    this.dirty.set(true);
  }

  onVisualInput(): void {
    const el = this.visualEditor()?.nativeElement;
    if (el) {
      this.bodyHtml.set(el.innerHTML);
      this.dirty.set(true);
    }
  }

  /** Switching to the visual editor runs the HTML through the server's clean-up first, so pasted code can't run in the admin. */
  setMode(mode: EditorMode): void {
    if (mode === this.mode()) {
      return;
    }
    if (mode === 'html') {
      this.onVisualInput();
      this.mode.set('html');
      return;
    }
    this.busy.set(true);
    this.api.preview({ subject: this.subject(), previewText: this.previewText(), bodyHtml: this.bodyHtml() }).subscribe({
      next: (p) => {
        this.bodyHtml.set(p.bodyHtml);
        this.mode.set('visual');
        this.busy.set(false);
        queueMicrotask(() => this.writeVisual(p.bodyHtml));
      },
      error: () => {
        this.busy.set(false);
        this.error.set('Could not switch editors. Try again.');
      },
    });
  }

  format(command: 'bold' | 'italic' | 'insertUnorderedList' | 'insertOrderedList' | 'removeFormat'): void {
    this.doc.execCommand(command);
    this.onVisualInput();
  }

  block(tag: 'H2' | 'H3' | 'P'): void {
    this.doc.execCommand('formatBlock', false, tag);
    this.onVisualInput();
  }

  link(): void {
    const url = prompt('Link address (https://…)');
    if (!url) {
      return;
    }
    if (!/^(https?:\/\/|mailto:)/i.test(url.trim())) {
      this.error.set('Links need to start with https:// or mailto:');
      return;
    }
    this.doc.execCommand('createLink', false, url.trim());
    this.onVisualInput();
  }

  uploadImage(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    const form = new FormData();
    form.append('file', file);
    this.busy.set(true);
    this.http.post<{ imageUrl?: string; url?: string }>(`${environment.apiUrl}/api/blog/upload-image`, form).subscribe({
      next: (res) => {
        this.busy.set(false);
        const url = res.imageUrl ?? res.url;
        if (!url) {
          this.error.set('The image uploaded but no address came back.');
          return;
        }
        this.insertHtml(`<p><img src="${esc(url)}" alt="" style="max-width:100%;height:auto;"></p>`);
      },
      error: () => {
        this.busy.set(false);
        this.error.set('The image could not be uploaded.');
      },
    });
  }

  togglePick(id: string): void {
    this.picked.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  insertPicked(): void {
    const ids = this.picked();
    const chosen = this.store.liveProducts().filter((p) => ids.has(p.id));
    if (chosen.length === 0) {
      return;
    }
    this.insertHtml(productCardsHtml(chosen, this.doc.location.origin));
    this.picked.set(new Set());
    this.pickerOpen.set(false);
  }

  /** Adds HTML at the end of the email (product cards, images). */
  private insertHtml(html: string): void {
    const next = `${this.bodyHtml()}\n${html}`;
    this.bodyHtml.set(next);
    this.dirty.set(true);
    if (this.mode() === 'visual') {
      this.writeVisual(next);
    }
  }

  private writeVisual(html: string): void {
    const el = this.visualEditor()?.nativeElement;
    if (el && el.innerHTML !== html) {
      // Only ever server-cleaned HTML or cards built here (escaped) reach this point.
      el.innerHTML = html;
    }
  }

  private payload() {
    if (this.mode() === 'visual') {
      this.onVisualInput();
    }
    return {
      subject: this.subject(),
      previewText: this.previewText(),
      bodyHtml: this.bodyHtml(),
      editorMode: this.mode(),
    };
  }

  /** Saves the draft (creating it the first time) and emits the saved newsletter. */
  private persist(): Observable<Newsletter> {
    const id = this.editingId();
    const req = id ? this.api.update(id, this.payload()) : this.api.create(this.payload());
    return req.pipe(
      map((n) => {
        this.editingId.set(n.id);
        this.editingStatus.set(n.status);
        this.bodyHtml.set(n.bodyHtml);
        this.dirty.set(false);
        return n;
      }),
    );
  }

  save(): void {
    this.run(this.persist(), () => {
      this.message.set('Draft saved.');
      this.refreshList();
    });
  }

  preview(): void {
    this.busy.set(true);
    this.error.set(null);
    const body = this.payload();
    this.api.preview({ subject: body.subject, previewText: body.previewText, bodyHtml: body.bodyHtml }).subscribe({
      next: (p) => {
        this.busy.set(false);
        // Shown in a sandboxed iframe (no scripts); the HTML is the server-cleaned email.
        this.previewHtml.set(this.sanitizer.bypassSecurityTrustHtml(p.html));
      },
      error: () => {
        this.busy.set(false);
        this.error.set('Could not build the preview.');
      },
    });
  }

  sendTest(): void {
    const email = this.testEmail().trim();
    if (!email) {
      this.error.set('Enter the address to send the test to.');
      return;
    }
    this.run(
      this.persist().pipe(switchMap((n) => this.api.sendTest(n.id, email))),
      () => this.message.set(`Test sent to ${email}. It can take a minute to arrive.`),
    );
  }

  sendNow(): void {
    const count = this.audience();
    if (!this.subject().trim()) {
      this.error.set('Add a subject line first.');
      return;
    }
    if (!confirm(`Send "${this.subject().trim()}" to ${count ?? 'all'} subscriber${count === 1 ? '' : 's'} now?\n\nThis can't be undone.`)) {
      return;
    }
    this.run(
      this.persist().pipe(switchMap((n) => this.api.send(n.id, null))),
      (n) => {
        this.editingStatus.set(n.status);
        this.message.set(`Sending to ${n.recipientCount} subscriber${n.recipientCount === 1 ? '' : 's'}. Progress shows under History.`);
        this.refreshList();
        this.startPolling();
      },
    );
  }

  schedule(): void {
    const local = this.scheduleAt();
    if (!local) {
      this.error.set('Pick a date and time to send.');
      return;
    }
    const when = new Date(local);
    if (Number.isNaN(when.getTime()) || when.getTime() < Date.now() + 2 * 60_000) {
      this.error.set('Pick a time at least a few minutes from now.');
      return;
    }
    const count = this.audience();
    const label = when.toLocaleString('en-GB', { dateStyle: 'full', timeStyle: 'short' });
    if (!confirm(`Send "${this.subject().trim()}" to ${count ?? 'all'} subscribers on ${label}?\n\nIt goes to whoever is subscribed at that time.`)) {
      return;
    }
    this.run(
      this.persist().pipe(switchMap((n) => this.api.send(n.id, when.toISOString()))),
      (n) => {
        this.editingStatus.set(n.status);
        this.message.set(`Scheduled for ${label}.`);
        this.refreshList();
      },
    );
  }

  cancelSchedule(): void {
    const id = this.editingId();
    if (!id) {
      return;
    }
    this.run(this.api.cancelSchedule(id), (n) => {
      this.editingStatus.set(n.status);
      this.scheduleAt.set('');
      this.message.set('Schedule cancelled. It is a draft again.');
      this.refreshList();
    });
  }

  deleteDraft(n: Newsletter): void {
    if (!confirm(`Delete the draft "${n.subject || '(no subject)'}"?`)) {
      return;
    }
    this.api.delete(n.id).subscribe({
      next: () => {
        if (this.editingId() === n.id) {
          this.newNewsletter();
        }
        this.refreshList();
      },
      error: (e) => this.error.set(e?.error?.error ?? 'Could not delete it.'),
    });
  }

  private run<T>(obs: Observable<T>, done: (value: T) => void): void {
    this.busy.set(true);
    this.message.set(null);
    this.error.set(null);
    obs.subscribe({
      next: (value) => {
        this.busy.set(false);
        done(value);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.error ?? 'Something went wrong. Please try again.');
      },
    });
  }

  // ---------- History ----------

  refreshList(): void {
    this.api.list().subscribe({
      next: (list) => {
        this.newsletters.set(list);
        if (!list.some((n) => n.status === 'Sending')) {
          this.stopPolling();
        }
        const current = list.find((n) => n.id === this.editingId());
        if (current) {
          this.editingStatus.set(current.status);
        }
      },
      error: () => {},
    });
  }

  private refreshAudience(): void {
    this.api.audience().subscribe({ next: (a) => this.audience.set(a.activeCount), error: () => {} });
  }

  private startPolling(): void {
    if (this.pollTimer) {
      return;
    }
    this.pollTimer = setInterval(() => this.refreshList(), 5000);
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  // ---------- Subscribers ----------

  loadSubscribers(): void {
    this.subscribersLoading.set(true);
    this.api.subscribers(this.subscriberSearch(), this.subscriberFilter()).subscribe({
      next: (rows) => {
        this.subscribers.set(rows);
        this.subscribersLoading.set(false);
      },
      error: () => this.subscribersLoading.set(false),
    });
  }

  setSubscriberFilter(filter: SubscriberFilter): void {
    this.subscriberFilter.set(filter);
    this.loadSubscribers();
  }

  exportCsv(): void {
    this.api.exportCsv(this.subscriberFilter()).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = this.doc.createElement('a');
        a.href = url;
        a.download = `subscribers-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      },
      error: () => this.error.set('Could not export the list.'),
    });
  }

  markUnsubscribed(s: AdminSubscriber): void {
    if (!confirm(`Mark ${s.email} as unsubscribed? They won't get any more newsletters.`)) {
      return;
    }
    this.api.markUnsubscribed(s.id).subscribe({
      next: () => {
        this.loadSubscribers();
        this.refreshAudience();
      },
    });
  }

  remove(s: AdminSubscriber): void {
    if (!confirm(`Remove ${s.email} from the list completely? Their signup record is deleted too.`)) {
      return;
    }
    this.api.removeSubscriber(s.id).subscribe({
      next: () => {
        this.loadSubscribers();
        this.refreshAudience();
      },
    });
  }

  private toLocalInput(utc: string): string {
    const d = new Date(utc);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  priceOf(p: Product): string {
    return p.salePrice != null && p.salePrice < p.price ? money(p.salePrice) : money(p.price);
  }
}
