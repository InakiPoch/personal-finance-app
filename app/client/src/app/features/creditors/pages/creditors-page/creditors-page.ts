import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { CreditorsService } from '../../creditors-service';
import { CreateCreditor } from '../../types/create-creditor';
import { Creditor } from '../../types/creditor';

type ListStatus = 'loading' | 'ready' | 'error';

type SubmitStatus = 'idle' | 'submitting' | 'error';

type AccountRow = FormGroup<{
  label: FormControl<string>;
  identifier: FormControl<string>;
}>;

type CreditorForm = FormGroup<{
  name: FormControl<string>;
  accounts: FormArray<AccountRow>;
}>;

@Component({
  selector: 'app-creditors-page',
  imports: [ReactiveFormsModule],
  templateUrl: './creditors-page.html',
  styleUrl: './creditors-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreditorsPage implements OnInit, OnDestroy {
  protected form!: CreditorForm;
  protected readonly creditors: WritableSignal<Creditor[]> = signal<Creditor[]>([]);
  protected readonly listStatus: WritableSignal<ListStatus> = signal<ListStatus>('loading');
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly createdCreditorId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.',
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly creditorsService: CreditorsService = inject(CreditorsService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Financing.InvalidCreditorName': 'Enter a name for the creditor.',
    'Http.BadRequest': 'The creditor could not be created — check the values and try again.',
    'Http.UnprocessableEntity': 'The creditor could not be created — check the values and try again.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected addAccountRow(): void {
    this.form.controls.accounts.push(this.createAccountRow());
  }

  protected removeAccountRow(index: number): void {
    this.form.controls.accounts.removeAt(index);
  }

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { name: string; accounts: Array<{ label: string; identifier: string }> } =
      this.form.getRawValue();
    const body: CreateCreditor = {
      name: raw.name.trim(),
      accounts: raw.accounts.map((account) => {
        const identifier = account.identifier.trim();
        return {
          label: account.label.trim(),
          identifier: identifier.length > 0 ? identifier : null
        };
      })
    };
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.creditorsService
      .create(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.createdCreditorId.set(result.creditorId);
          this.resetCreditorForm();
          this.submitStatus.set('idle');
          this.loadCreditors();
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The creditor could not be created.';
  }

  private loadCreditors(): void {
    this.listStatus.set('loading');
    this.creditorsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: Creditor[]) => {
          this.creditors.set(rows);
          this.listStatus.set('ready');
        },
        error: () => this.listStatus.set('error')
      }
    );
  }

  private createAccountRow(): AccountRow {
    return this.fb.group({
      label: this.fb.nonNullable.control('', { validators: Validators.required }),
      identifier: this.fb.nonNullable.control('')
    });
  }

  private resetCreditorForm(): void {
    this.form.controls.accounts.clear();
    this.form.controls.accounts.push(this.createAccountRow());
    this.form.reset({ name: '' });
  }

  private initCreditorForm(): void {
    this.form = this.fb.group({
      name: this.fb.nonNullable.control('', { validators: Validators.required }),
      accounts: this.fb.array<AccountRow>([this.createAccountRow()])
    });
  }

  ngOnInit(): void {
    this.initCreditorForm();
    this.loadCreditors();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
