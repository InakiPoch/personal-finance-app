import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  Signal,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { FinancingService } from '../../financing-service';
import { MonthlyStatementSummary } from '../../types/monthly-statement-summary';
import { StatementsTable } from './statements-table';

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error';

type CardForm = FormGroup<{
  cardId: FormControl<string>;
}>;

@Component({
  selector: 'app-statements-page',
  imports: [ReactiveFormsModule, StatementsTable],
  templateUrl: './statements-page.html',
  styleUrl: './statements-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StatementsPage implements OnInit, OnDestroy {
  protected form!: CardForm;
  protected readonly statements: WritableSignal<MonthlyStatementSummary[]> =
    signal<MonthlyStatementSummary[]>([]);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('idle');
  protected readonly creditCards: Signal<Instrument[]> = computed(() =>
    this.instruments().filter((instrument: Instrument) => instrument.type === 'credit')
  );

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly router: Router = inject(Router);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected openStatement(statementId: string): void {
    this.router.navigate(['financing', 'statements', statementId]);
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
    .subscribe((rows: Instrument[]) => this.instruments.set(rows));
  }

  private loadStatements(cardId: string): void {
    if(cardId === '') {
      this.statements.set([]);
      this.loadStatus.set('idle');
      return;
    }
    this.loadStatus.set('loading');
    this.financing
      .listStatements(cardId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: MonthlyStatementSummary[]) => {
          this.statements.set(rows);
          this.loadStatus.set('ready');
        },
        error: () => this.loadStatus.set('error')
      }
    );
  }

  private initCardForm(): void {
    this.form = this.fb.group({
      cardId: this.fb.nonNullable.control(''),
    });
    this.form.controls.cardId.valueChanges
      .pipe(takeUntil(this.destroy$))
    .subscribe((cardId: string) => this.loadStatements(cardId));
  }

  ngOnInit(): void {
    this.initCardForm();
    this.loadInstruments();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
