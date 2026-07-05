import { DatePipe } from '@angular/common';
import {
  Component,
  computed,
  inject,
  signal,
  OnInit,
  ChangeDetectionStrategy,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CalendarConfig,
  CalendarService,
  CompleteObligationRequest,
  CreateObligationRequest,
  LiabilityObligation,
  LiabilityStatus,
} from '../../services/calendar.service';

const MONTH_LABELS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

interface CalendarDay {
  date: Date;
  isoDate: string; // YYYY-MM-DD
  inMonth: boolean;
  isToday: boolean;
  chips: ObligationChip[];
}

interface ObligationChip {
  obligation: LiabilityObligation;
  kindClass: 'scheduled' | 'pending' | 'overdue' | 'complete' | 'waived' | 'submitted';
  isScheduledRender: boolean;
}

type Mode = 'schedule' | 'complete' | null;
type CreateMode = 'closed' | 'open';

/**
 * Month-grid calendar of regulatory obligations. Each obligation renders as a chip on the most
 * relevant day: its scheduled work-session time if set; otherwise its due date; once complete,
 * the completion date. Clicking a chip opens the side panel to assign a time (creates an email
 * reminder), mark complete (with evidence), waive (N/A), or re-open. Statutory rows are
 * auto-generated; free-form events (kind 'other') can be added and deleted.
 */
@Component({
  selector: 'app-admin-calendar',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './admin-calendar.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './admin-calendar.component.scss',
})
export class AdminCalendarComponent implements OnInit {
  private readonly calendar = inject(CalendarService);
  private readonly fb = inject(FormBuilder);

  protected readonly obligations = signal<LiabilityObligation[]>([]);
  protected readonly loading = signal(true);
  protected readonly working = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly config = signal<CalendarConfig | null>(null);
  protected readonly selected = signal<LiabilityObligation | null>(null);
  protected readonly mode = signal<Mode>(null);
  protected readonly createMode = signal<CreateMode>('closed');

  protected readonly viewYear = signal(new Date().getUTCFullYear());
  protected readonly viewMonth = signal(new Date().getUTCMonth()); // 0-11

  protected readonly scheduleForm = this.fb.nonNullable.group({
    scheduledFor: this.fb.nonNullable.control(this.defaultScheduleLocal(), {
      validators: [Validators.required],
    }),
  });

  protected readonly completeForm = this.fb.nonNullable.group({
    submissionReference: this.fb.nonNullable.control(''),
    paidAmountMajor: this.fb.nonNullable.control<number | null>(null),
    paymentReference: this.fb.nonNullable.control(''),
    paidAt: this.fb.nonNullable.control(''),
    filedAt: this.fb.nonNullable.control(this.todayIsoDate()),
    notes: this.fb.nonNullable.control(''),
  });

  protected readonly createForm = this.fb.nonNullable.group({
    title: this.fb.nonNullable.control('', {
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    dueDate: this.fb.nonNullable.control(this.todayIsoDate(), {
      validators: [Validators.required],
    }),
    scheduledFor: this.fb.nonNullable.control(''),
    notes: this.fb.nonNullable.control(''),
  });

  protected readonly monthLabel = computed(
    () => `${MONTH_LABELS[this.viewMonth()]} ${this.viewYear()}`,
  );

  protected readonly weeks = computed<CalendarDay[][]>(() => this.buildGrid());

  protected readonly upcoming = computed(() => {
    const todayIso = this.todayIsoDate();
    return this.obligations()
      .filter((o) => o.status !== 'complete' && o.status !== 'waived')
      .sort((a, b) => a.dueDate.localeCompare(b.dueDate))
      .slice(0, 20)
      .map((o) => ({ obligation: o, isOverdue: o.isOverdue || o.dueDate < todayIso }));
  });

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [obligations, config] = await Promise.all([
        this.calendar.list({ from: this.windowStartIso(), to: this.windowEndIso() }),
        this.calendar.config(),
      ]);
      this.obligations.set(obligations);
      this.config.set(config);
      this.error.set(null);
    } catch {
      this.error.set(
        'Could not load the calendar — check the API is up and you hold the Admin role.',
      );
    } finally {
      this.loading.set(false);
    }
  }

  protected prevMonth(): void {
    const m = this.viewMonth();
    if (m === 0) {
      this.viewMonth.set(11);
      this.viewYear.update((y) => y - 1);
    } else {
      this.viewMonth.set(m - 1);
    }
    void this.load();
  }

  protected nextMonth(): void {
    const m = this.viewMonth();
    if (m === 11) {
      this.viewMonth.set(0);
      this.viewYear.update((y) => y + 1);
    } else {
      this.viewMonth.set(m + 1);
    }
    void this.load();
  }

  protected goToToday(): void {
    const now = new Date();
    this.viewYear.set(now.getUTCFullYear());
    this.viewMonth.set(now.getUTCMonth());
    void this.load();
  }

  protected openObligation(o: LiabilityObligation): void {
    this.selected.set(o);
    this.mode.set(null);
    this.message.set(null);
  }

  protected closePanel(): void {
    this.selected.set(null);
    this.mode.set(null);
  }

  protected beginSchedule(): void {
    const o = this.selected();
    if (!o) {
      return;
    }
    this.scheduleForm.reset({
      scheduledFor: o.scheduledFor
        ? this.toLocalInput(o.scheduledFor)
        : this.defaultScheduleLocal(),
    });
    this.mode.set('schedule');
  }

  protected beginComplete(): void {
    const o = this.selected();
    if (!o) {
      return;
    }
    this.completeForm.reset({
      submissionReference: o.submissionReference ?? '',
      paidAmountMajor: o.paidAmountMinor !== null ? o.paidAmountMinor / 100 : null,
      paymentReference: o.paymentReference ?? '',
      paidAt: o.paidAt ? o.paidAt.slice(0, 10) : '',
      filedAt: o.filedAt ? o.filedAt.slice(0, 10) : this.todayIsoDate(),
      notes: o.notes ?? '',
    });
    this.mode.set('complete');
  }

  protected async submitSchedule(): Promise<void> {
    const o = this.selected();
    if (!o || this.scheduleForm.invalid || this.working()) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      const v = this.scheduleForm.getRawValue();
      const iso = new Date(v.scheduledFor).toISOString();
      const updated = await this.calendar.schedule(o.id, { scheduledFor: iso });
      this.replaceObligation(updated);
      this.selected.set(updated);
      this.mode.set(null);
      this.message.set('Scheduled — an email reminder will fire at that time.');
    } catch {
      this.error.set('Could not schedule the obligation — try again.');
    } finally {
      this.working.set(false);
    }
  }

  protected async unschedule(): Promise<void> {
    const o = this.selected();
    if (!o || this.working()) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      const updated = await this.calendar.unschedule(o.id);
      this.replaceObligation(updated);
      this.selected.set(updated);
      this.message.set('Schedule cleared — reminder removed.');
    } catch {
      this.error.set('Could not unschedule.');
    } finally {
      this.working.set(false);
    }
  }

  protected async submitComplete(): Promise<void> {
    const o = this.selected();
    if (!o || this.working()) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      const v = this.completeForm.getRawValue();
      const body: CompleteObligationRequest = {
        submissionReference: emptyToNull(v.submissionReference),
        paidAmountMinor:
          v.paidAmountMajor !== null && v.paidAmountMajor !== undefined
            ? Math.round(v.paidAmountMajor * 100)
            : null,
        paymentReference: emptyToNull(v.paymentReference),
        paidAt: v.paidAt ? new Date(v.paidAt).toISOString() : null,
        filedAt: v.filedAt ? new Date(v.filedAt).toISOString() : null,
        notes: emptyToNull(v.notes),
      };
      const updated = await this.calendar.complete(o.id, body);
      this.replaceObligation(updated);
      this.selected.set(updated);
      this.mode.set(null);
      this.message.set('Marked complete. Evidence recorded.');
    } catch {
      this.error.set('Could not mark complete — try again.');
    } finally {
      this.working.set(false);
    }
  }

  protected async waive(): Promise<void> {
    const o = this.selected();
    if (!o || this.working()) {
      return;
    }
    if (!confirm(`Mark "${o.title}" as not applicable?`)) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      const updated = await this.calendar.waive(o.id);
      this.replaceObligation(updated);
      this.selected.set(updated);
      this.message.set('Marked N/A.');
    } catch {
      this.error.set('Could not waive.');
    } finally {
      this.working.set(false);
    }
  }

  protected async reopen(): Promise<void> {
    const o = this.selected();
    if (!o || this.working()) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      const updated = await this.calendar.reopen(o.id);
      this.replaceObligation(updated);
      this.selected.set(updated);
      this.message.set('Re-opened.');
    } catch {
      this.error.set('Could not re-open.');
    } finally {
      this.working.set(false);
    }
  }

  protected openCreate(): void {
    this.createForm.reset({
      title: '',
      dueDate: this.todayIsoDate(),
      scheduledFor: '',
      notes: '',
    });
    this.createMode.set('open');
    this.message.set(null);
  }

  protected closeCreate(): void {
    this.createMode.set('closed');
  }

  protected async submitCreate(): Promise<void> {
    if (this.createForm.invalid || this.working()) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      const v = this.createForm.getRawValue();
      const body: CreateObligationRequest = {
        title: v.title.trim(),
        dueDate: v.dueDate,
        scheduledFor: v.scheduledFor ? new Date(v.scheduledFor).toISOString() : null,
        notes: v.notes && v.notes.trim() ? v.notes.trim() : null,
      };
      const created = await this.calendar.create(body);
      this.obligations.update((all) => [...all, created]);
      this.createMode.set('closed');
      this.selected.set(created);
      this.message.set(
        body.scheduledFor
          ? 'Event added — an email reminder will fire at the scheduled time.'
          : 'Event added.',
      );
    } catch {
      this.error.set('Could not save the event — try again.');
    } finally {
      this.working.set(false);
    }
  }

  protected async deleteSelected(): Promise<void> {
    const o = this.selected();
    if (!o || this.working()) {
      return;
    }
    if (o.kind !== 'other') {
      return;
    }
    if (!confirm(`Delete "${o.title}"? This can't be undone.`)) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    try {
      await this.calendar.remove(o.id);
      this.obligations.update((all) => all.filter((x) => x.id !== o.id));
      this.selected.set(null);
      this.mode.set(null);
      this.message.set('Event deleted.');
    } catch {
      this.error.set('Could not delete the event.');
    } finally {
      this.working.set(false);
    }
  }

  protected async copySubscribeUrl(): Promise<void> {
    const url = this.config()?.icalSubscribeUrl;
    if (!url) {
      return;
    }
    try {
      await navigator.clipboard.writeText(url);
      this.message.set(
        'Subscribe URL copied — paste it into Google Calendar / Outlook / iPhone Settings → Calendar.',
      );
    } catch {
      this.message.set('Copy failed — select the URL manually.');
    }
  }

  protected formatMoney(amountMinor: number | null, currency: string): string {
    if (amountMinor === null || amountMinor === undefined) {
      return '';
    }
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: currency.toUpperCase(),
      maximumFractionDigits: 2,
    }).format(amountMinor / 100);
  }

  protected statusLabel(status: LiabilityStatus): string {
    if (status === 'waived') {
      return 'N/A';
    }
    return status.charAt(0).toUpperCase() + status.slice(1);
  }

  private replaceObligation(updated: LiabilityObligation): void {
    this.obligations.update((all) => all.map((o) => (o.id === updated.id ? updated : o)));
  }

  private buildGrid(): CalendarDay[][] {
    const year = this.viewYear();
    const month = this.viewMonth();
    const firstOfMonth = new Date(Date.UTC(year, month, 1));
    const dayOfWeek = (firstOfMonth.getUTCDay() + 6) % 7; // Monday-start
    const gridStart = new Date(Date.UTC(year, month, 1 - dayOfWeek));

    const byDate = this.chipsByDate();
    const todayIso = this.todayIsoDate();

    const weeks: CalendarDay[][] = [];
    for (let w = 0; w < 6; w++) {
      const row: CalendarDay[] = [];
      for (let d = 0; d < 7; d++) {
        const date = new Date(gridStart);
        date.setUTCDate(gridStart.getUTCDate() + w * 7 + d);
        const iso = this.isoFor(date);
        row.push({
          date,
          isoDate: iso,
          inMonth: date.getUTCMonth() === month,
          isToday: iso === todayIso,
          chips: byDate.get(iso) ?? [],
        });
      }
      weeks.push(row);
    }
    return weeks;
  }

  private chipsByDate(): Map<string, ObligationChip[]> {
    const result = new Map<string, ObligationChip[]>();
    const todayIso = this.todayIsoDate();
    for (const o of this.obligations()) {
      let renderDate: string;
      let isScheduledRender = false;
      if (o.status === 'waived') {
        renderDate = o.dueDate;
      } else if (o.status === 'complete') {
        renderDate = (o.filedAt ?? o.paidAt ?? o.dueDate + 'T00:00:00Z').slice(0, 10);
      } else if (o.scheduledFor) {
        renderDate = o.scheduledFor.slice(0, 10);
        isScheduledRender = true;
      } else {
        renderDate = o.dueDate;
      }

      const chip: ObligationChip = {
        obligation: o,
        kindClass: this.classFor(o, todayIso),
        isScheduledRender,
      };
      const list = result.get(renderDate);
      if (list) {
        list.push(chip);
      } else {
        result.set(renderDate, [chip]);
      }
    }
    return result;
  }

  private classFor(o: LiabilityObligation, todayIso: string): ObligationChip['kindClass'] {
    if (o.status === 'complete') {
      return 'complete';
    }
    if (o.status === 'waived') {
      return 'waived';
    }
    if (o.status === 'submitted' || o.status === 'paid') {
      return 'submitted';
    }
    if (o.dueDate < todayIso) {
      return 'overdue';
    }
    if (o.scheduledFor) {
      return 'scheduled';
    }
    return 'pending';
  }

  private isoFor(d: Date): string {
    return d.toISOString().slice(0, 10);
  }

  private todayIsoDate(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private windowStartIso(): string {
    return new Date(Date.UTC(this.viewYear(), this.viewMonth() - 1, 1)).toISOString().slice(0, 10);
  }

  private windowEndIso(): string {
    return new Date(Date.UTC(this.viewYear(), this.viewMonth() + 2, 0)).toISOString().slice(0, 10);
  }

  private defaultScheduleLocal(): string {
    const d = new Date();
    d.setHours(9, 0, 0, 0);
    d.setDate(d.getDate() + 1);
    return this.toDatetimeLocal(d);
  }

  private toLocalInput(iso: string): string {
    return this.toDatetimeLocal(new Date(iso));
  }

  private toDatetimeLocal(d: Date): string {
    const pad = (n: number): string => n.toString().padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}

function emptyToNull(s: string): string | null {
  return s && s.trim() ? s.trim() : null;
}
