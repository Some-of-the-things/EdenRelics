import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface WorklistItem {
  id: string;
  type: 'fabric' | 'issue';
  name: string;
  slug: string;
  status: 'Draft' | 'AiDrafted' | 'ExpertApproved';
  isPublished: boolean;
  needsAction: boolean;
  targetKeywords: string[];
  reviewNotes: string;
  lastReviewedUtc: string | null;
  updatedAtUtc: string;
}

interface FabricDto {
  id: string;
  slug: string;
  name: string;
  alsoKnownAs: string[];
  targetKeywords: string[];
  intro: string;
  fiberContent: string;
  howToIdentify: string;
  washing: string;
  drying: string;
  ironing: string;
  storing: string;
  vintageCautions: string;
  dos: string[];
  donts: string[];
  metaTitle: string;
  metaDescription: string;
  status: string;
  reviewNotes: string;
  reviewedBy: string | null;
  lastReviewedUtc: string | null;
  isPublished: boolean;
}

interface GuidanceDto {
  id: string;
  fabricId: string;
  issueId: string;
  safety: string;
  shortAnswer: string;
  specificMethod: string;
  status: string;
}

interface IssueDto {
  id: string;
  slug: string;
  name: string;
  alsoKnownAs: string[];
  targetKeywords: string[];
  causes: string;
  generalMethod: string;
  whatNotToDo: string;
  whenToSeeAPro: string;
  metaTitle: string;
  metaDescription: string;
  status: string;
  reviewNotes: string;
  reviewedBy: string | null;
  lastReviewedUtc: string | null;
  isPublished: boolean;
}

@Component({
  selector: 'app-admin-care',
  imports: [FormsModule, DatePipe],
  templateUrl: './admin-care.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './admin-care.component.scss',
})
export class AdminCareComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  readonly worklist = signal<WorklistItem[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly aiAvailable = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  // Editor state. We hold the editing record as a plain object that ngModel mutates
  // in place; the signal flips presence on/off and triggers list re-render.
  readonly editingType = signal<'fabric' | 'issue' | null>(null);
  fabric: FabricDto | null = null;
  issue: IssueDto | null = null;

  // List fields edited as newline-separated text.
  targetKeywordsText = '';
  alsoKnownAsText = '';
  dosText = '';
  dontsText = '';

  ngOnInit(): void {
    this.loadWorklist();
    this.http.get<{ available: boolean }>(`${this.api}/api/care/admin/ai-available`).subscribe({
      next: (r) => this.aiAvailable.set(r.available),
      error: () => {},
    });
  }

  get editingId(): string | null {
    return (this.editingType() === 'fabric' ? this.fabric?.id : this.issue?.id) ?? null;
  }

  generateDraft(): void {
    const type = this.editingType();
    const id = this.editingId;
    if (!type || !id) {
      this.error.set('Save the entry before generating a draft.');
      return;
    }
    if (
      !confirm(
        'Generate an AI draft? This replaces the current content fields (target terms and review notes are kept). A human review is still required before it can be published.',
      )
    ) {
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.message.set('');
    this.http
      .post<FabricDto | IssueDto>(`${this.api}/api/care/admin/${type}/${id}/generate`, {})
      .subscribe({
        next: (updated) => {
          if (type === 'fabric') {
            this.fabric = updated as FabricDto;
            this.dosText = (this.fabric.dos ?? []).join('\n');
            this.dontsText = (this.fabric.donts ?? []).join('\n');
          } else {
            this.issue = updated as IssueDto;
          }
          this.saving.set(false);
          this.message.set('AI draft generated — please review before publishing.');
          this.loadWorklist();
        },
        error: () => {
          this.saving.set(false);
          this.error.set('Draft generation failed.');
        },
      });
  }

  loadWorklist(): void {
    this.loading.set(true);
    this.error.set('');
    this.http.get<WorklistItem[]>(`${this.api}/api/care/admin/worklist`).subscribe({
      next: (items) => {
        this.worklist.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load the care worklist.');
        this.loading.set(false);
      },
    });
  }

  get outstandingCount(): number {
    return this.worklist().filter((i) => i.needsAction).length;
  }

  // --- Finder guidance overrides ---
  get fabricOptions(): WorklistItem[] {
    return this.worklist().filter((i) => i.type === 'fabric');
  }
  get issueOptions(): WorklistItem[] {
    return this.worklist().filter((i) => i.type === 'issue');
  }
  guidanceFabricId = '';
  guidanceIssueId = '';
  guidanceForm: { safety: string; shortAnswer: string; specificMethod: string } = {
    safety: 'Unknown',
    shortAnswer: '',
    specificMethod: '',
  };
  readonly guidanceLoaded = signal(false);
  readonly guidanceStatus = signal('');
  readonly guidanceMsg = signal('');

  loadGuidance(): void {
    if (!this.guidanceFabricId || !this.guidanceIssueId) {
      return;
    }
    this.guidanceMsg.set('');
    this.http
      .get<GuidanceDto>(
        `${this.api}/api/care/admin/guidance?fabricId=${this.guidanceFabricId}&issueId=${this.guidanceIssueId}`,
      )
      .subscribe({
        next: (g) => {
          if (g) {
            this.guidanceForm = {
              safety: g.safety,
              shortAnswer: g.shortAnswer,
              specificMethod: g.specificMethod,
            };
            this.guidanceStatus.set(g.status);
          } else {
            this.guidanceForm = { safety: 'Unknown', shortAnswer: '', specificMethod: '' };
            this.guidanceStatus.set('none');
          }
          this.guidanceLoaded.set(true);
        },
        error: () => {
          this.guidanceForm = { safety: 'Unknown', shortAnswer: '', specificMethod: '' };
          this.guidanceStatus.set('none');
          this.guidanceLoaded.set(true);
        },
      });
  }

  saveGuidance(approved: boolean): void {
    this.guidanceMsg.set('');
    this.http
      .post<GuidanceDto>(`${this.api}/api/care/admin/guidance`, {
        fabricId: this.guidanceFabricId,
        issueId: this.guidanceIssueId,
        safety: this.guidanceForm.safety,
        shortAnswer: this.guidanceForm.shortAnswer,
        specificMethod: this.guidanceForm.specificMethod,
        approved,
      })
      .subscribe({
        next: (g) => {
          this.guidanceStatus.set(g.status);
          this.guidanceMsg.set(approved ? 'Saved & live in the finder.' : 'Saved as draft.');
        },
        error: () => this.guidanceMsg.set('Save failed.'),
      });
  }

  /** Review notes live on whichever record is being edited; proxy so the shared brief panel can bind. */
  get reviewNotesModel(): string {
    return (
      (this.editingType() === 'fabric' ? this.fabric?.reviewNotes : this.issue?.reviewNotes) ?? ''
    );
  }
  set reviewNotesModel(value: string) {
    if (this.editingType() === 'fabric' && this.fabric) {
      this.fabric.reviewNotes = value;
    } else if (this.issue) {
      this.issue.reviewNotes = value;
    }
  }

  open(item: WorklistItem): void {
    this.message.set('');
    this.error.set('');
    if (item.type === 'fabric') {
      this.http.get<FabricDto>(`${this.api}/api/care/admin/fabric/${item.id}`).subscribe({
        next: (f) => {
          this.fabric = f;
          this.issue = null;
          this.targetKeywordsText = f.targetKeywords.join('\n');
          this.alsoKnownAsText = f.alsoKnownAs.join('\n');
          this.dosText = f.dos.join('\n');
          this.dontsText = f.donts.join('\n');
          this.editingType.set('fabric');
        },
        error: () => this.error.set('Failed to load entry.'),
      });
    } else {
      this.http.get<IssueDto>(`${this.api}/api/care/admin/issue/${item.id}`).subscribe({
        next: (i) => {
          this.issue = i;
          this.fabric = null;
          this.targetKeywordsText = i.targetKeywords.join('\n');
          this.alsoKnownAsText = i.alsoKnownAs.join('\n');
          this.editingType.set('issue');
        },
        error: () => this.error.set('Failed to load entry.'),
      });
    }
  }

  newFabric(): void {
    this.fabric = blankFabric();
    this.issue = null;
    this.targetKeywordsText = this.alsoKnownAsText = this.dosText = this.dontsText = '';
    this.message.set('');
    this.error.set('');
    this.editingType.set('fabric');
  }

  newIssue(): void {
    this.issue = blankIssue();
    this.fabric = null;
    this.targetKeywordsText = this.alsoKnownAsText = '';
    this.message.set('');
    this.error.set('');
    this.editingType.set('issue');
  }

  closeEditor(): void {
    this.editingType.set(null);
    this.fabric = null;
    this.issue = null;
  }

  saveFabric(): void {
    if (!this.fabric) {
      return;
    }
    const body = {
      slug: this.fabric.slug || null,
      name: this.fabric.name,
      alsoKnownAs: lines(this.alsoKnownAsText),
      targetKeywords: lines(this.targetKeywordsText),
      intro: this.fabric.intro,
      fiberContent: this.fabric.fiberContent,
      howToIdentify: this.fabric.howToIdentify,
      washing: this.fabric.washing,
      drying: this.fabric.drying,
      ironing: this.fabric.ironing,
      storing: this.fabric.storing,
      vintageCautions: this.fabric.vintageCautions,
      dos: lines(this.dosText),
      donts: lines(this.dontsText),
      metaTitle: this.fabric.metaTitle,
      metaDescription: this.fabric.metaDescription,
      reviewNotes: this.fabric.reviewNotes,
    };
    this.saving.set(true);
    this.error.set('');
    const isNew = !this.fabric.id;
    const req = isNew
      ? this.http.post<FabricDto>(`${this.api}/api/care/admin/fabric`, body)
      : this.http.put<FabricDto>(`${this.api}/api/care/admin/fabric/${this.fabric.id}`, body);
    req.subscribe({
      next: (f) => {
        this.fabric = f;
        this.saving.set(false);
        this.message.set('Saved.');
        this.loadWorklist();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Save failed.');
      },
    });
  }

  saveIssue(): void {
    if (!this.issue) {
      return;
    }
    const body = {
      slug: this.issue.slug || null,
      name: this.issue.name,
      alsoKnownAs: lines(this.alsoKnownAsText),
      targetKeywords: lines(this.targetKeywordsText),
      causes: this.issue.causes,
      generalMethod: this.issue.generalMethod,
      whatNotToDo: this.issue.whatNotToDo,
      whenToSeeAPro: this.issue.whenToSeeAPro,
      metaTitle: this.issue.metaTitle,
      metaDescription: this.issue.metaDescription,
      reviewNotes: this.issue.reviewNotes,
    };
    this.saving.set(true);
    this.error.set('');
    const isNew = !this.issue.id;
    const req = isNew
      ? this.http.post<IssueDto>(`${this.api}/api/care/admin/issue`, body)
      : this.http.put<IssueDto>(`${this.api}/api/care/admin/issue/${this.issue.id}`, body);
    req.subscribe({
      next: (i) => {
        this.issue = i;
        this.saving.set(false);
        this.message.set('Saved.');
        this.loadWorklist();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Save failed.');
      },
    });
  }

  setPublished(published: boolean): void {
    const type = this.editingType();
    const id = type === 'fabric' ? this.fabric?.id : this.issue?.id;
    if (!type || !id) {
      this.error.set('Save the entry before publishing.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.http
      .post<FabricDto | IssueDto>(`${this.api}/api/care/admin/${type}/${id}/publish`, { published })
      .subscribe({
        next: (updated) => {
          if (type === 'fabric') {
            this.fabric = updated as FabricDto;
          } else {
            this.issue = updated as IssueDto;
          }
          this.saving.set(false);
          this.message.set(published ? 'Published — now live.' : 'Unpublished.');
          this.loadWorklist();
        },
        error: () => {
          this.saving.set(false);
          this.error.set('Publish failed.');
        },
      });
  }

  publicUrl(): string {
    if (this.editingType() === 'fabric' && this.fabric?.slug) {
      return `/care/fabric/${this.fabric.slug}`;
    }
    if (this.editingType() === 'issue' && this.issue?.slug) {
      return `/care/problem/${this.issue.slug}`;
    }
    return '';
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'AiDrafted':
        return 'Needs review';
      case 'ExpertApproved':
        return 'Approved';
      default:
        return 'Draft';
    }
  }
}

function lines(text: string): string[] {
  return text
    .split('\n')
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
}

function blankFabric(): FabricDto {
  return {
    id: '',
    slug: '',
    name: '',
    alsoKnownAs: [],
    targetKeywords: [],
    intro: '',
    fiberContent: '',
    howToIdentify: '',
    washing: '',
    drying: '',
    ironing: '',
    storing: '',
    vintageCautions: '',
    dos: [],
    donts: [],
    metaTitle: '',
    metaDescription: '',
    status: 'Draft',
    reviewNotes: '',
    reviewedBy: null,
    lastReviewedUtc: null,
    isPublished: false,
  };
}

function blankIssue(): IssueDto {
  return {
    id: '',
    slug: '',
    name: '',
    alsoKnownAs: [],
    targetKeywords: [],
    causes: '',
    generalMethod: '',
    whatNotToDo: '',
    whenToSeeAPro: '',
    metaTitle: '',
    metaDescription: '',
    status: 'Draft',
    reviewNotes: '',
    reviewedBy: null,
    lastReviewedUtc: null,
    isPublished: false,
  };
}
