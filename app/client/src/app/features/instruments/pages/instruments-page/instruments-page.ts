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
import { Subject, takeUntil } from 'rxjs';
import { InstrumentRegistryService } from '../../../../core/registry/instrument-registry-service';
import { AppError } from '../../../../core/types/app-error';
import { InstrumentType } from '../../../../core/types/instrument-type';
import { RegisteredInstrument } from '../../../../core/types/registered-instrument';
import { InstrumentsService } from '../../instruments-service';
import { CreateInstrument } from '../../types/create-instrument';
import { InstrumentCreated } from '../../types/instrument-created';
import { creditRequiresCutoff } from '../../validation-helpers';

type SubmitStatus = 'idle' | 'submitting' | 'error';

type InstrumentForm = FormGroup<{
  type: FormControl<InstrumentType>;
  name: FormControl<string>;
  cutoffDate: FormControl<number | null>;
}>;

@Component({
  selector: 'app-instruments-page',
  imports: [ReactiveFormsModule],
  templateUrl: './instruments-page.html',
  styleUrl: './instruments-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstrumentsPage implements OnInit, OnDestroy {
  protected form!: InstrumentForm;
  protected readonly registry: InstrumentRegistryService = inject(InstrumentRegistryService);
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly nameErrors: Record<string, string> = { required: 'Name is required.' };
  protected readonly cutoffErrors: Record<string, string> = { creditCutoff: 'Credit cards need a cutoff day between 1 and 31.'};

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly instruments: InstrumentsService = inject(InstrumentsService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Instruments.UnknownType': 'That instrument type is not supported.',
    'Http.BadRequest': 'The instrument could not be created — check the values and try again.',
    'Http.Conflict': 'That instrument already exists.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.',
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { type: InstrumentType; name: string; cutoffDate: number | null } =
      this.form.getRawValue();
    const body: CreateInstrument = {
      type: raw.type,
      name: raw.name.trim(),
      ...(raw.type === 'credit' && raw.cutoffDate !== null ? { cutoffDate: raw.cutoffDate } : {}),
    };
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.instruments
      .create(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (created: InstrumentCreated) => {
          const registered: RegisteredInstrument = {
            id: created.id,
            type: created.type,
            name: body.name,
            ...(body.cutoffDate !== undefined ? { cutoffDate: body.cutoffDate } : {}),
          };
          this.registry.add(registered);
          this.form.reset({ type: 'debit', name: '', cutoffDate: null });
          this.submitStatus.set('idle');
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        },
      }
    );
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The instrument could not be created.';
  }

  private initInstrumentForm(): void {
    this.form = this.fb.group(
      {
        type: this.fb.nonNullable.control<InstrumentType>('debit', {
          validators: Validators.required,
        }),
        name: this.fb.nonNullable.control('', { validators: Validators.required }),
        cutoffDate: this.fb.control<number | null>(null),
      },
      { validators: creditRequiresCutoff },
    );
  }

  ngOnInit(): void {
    this.initInstrumentForm();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
