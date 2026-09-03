import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { formatArs } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../../reports/reports-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { CreateParty } from '../../types/create-party';
import { PartyResult } from '../../types/party-result';
import { PartiesService } from '../../parties-service';

type ListStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'error';

type PartyForm = FormGroup<{
  name: FormControl<string>;
}>;

@Component({
  selector: 'app-parties-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './parties-page.html',
  styleUrl: './parties-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartiesPage implements OnInit, OnDestroy {
  protected form!: PartyForm;
  protected readonly formatArs: (value: Money) => string = formatArs;
  protected readonly listStatus: WritableSignal<ListStatus> = signal<ListStatus>('loading');
  protected readonly parties: WritableSignal<PartyDebtRow[]> = signal<PartyDebtRow[]>([]);
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly createdPartyId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly reports: ReportsService = inject(ReportsService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Parties.InvalidName': 'Enter a name for the party.',
    'Http.UnprocessableEntity': 'The party could not be created — check the name and try again.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { name: string } = this.form.getRawValue();
    const body: CreateParty = { name: raw.name.trim() };
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.partiesService
      .create(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PartyResult) => {
          this.createdPartyId.set(result.id);
          this.form.reset({ name: '' });
          this.submitStatus.set('idle');
          this.loadParties();
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  protected balanceHint(row: PartyDebtRow): string {
    if(row.netBalanceMinorUnits > 0) {
      return 'They owe you';
    }
    if(row.netBalanceMinorUnits < 0) {
      return 'You owe them';
    }
    return 'Settled up';
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The party could not be created.';
  }

  private loadParties(): void {
    this.listStatus.set('loading');
    this.reports
      .debtSummary()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: PartyDebtRow[]) => {
          this.parties.set(rows);
          this.listStatus.set('ready');
        },
        error: () => this.listStatus.set('error')
      }
    );
  }

  private initPartyForm(): void {
    this.form = this.fb.group({
      name: this.fb.nonNullable.control('', { validators: Validators.required })
    });
  }

  ngOnInit(): void {
    this.initPartyForm();
    this.loadParties();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
