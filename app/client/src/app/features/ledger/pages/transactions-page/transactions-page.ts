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
import { LedgerService } from '../../ledger-service';
import { TransactionRow } from '../../types/transaction-row';
import { TransactionsTable } from './transactions-table';

type LoadStatus = 'loading' | 'ready' | 'error';

type FeedFilterForm = FormGroup<{
  accountId: FormControl<string>;
  from: FormControl<string>;
  to: FormControl<string>;
}>;

@Component({
  selector: 'app-transactions-page',
  imports: [ReactiveFormsModule, TransactionsTable],
  templateUrl: './transactions-page.html',
  styleUrl: './transactions-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TransactionsPage implements OnInit, OnDestroy {
  protected form!: FeedFilterForm;
  protected readonly transactions: WritableSignal<TransactionRow[]> = signal<TransactionRow[]>([]);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly accounts: Signal<Instrument[]> = computed(() =>
    this.instruments().filter((instrument: Instrument) => instrument.type !== 'credit')
  );

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly router: Router = inject(Router);
  private readonly ledger: LedgerService = inject(LedgerService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected openReverse(transactionId: string): void {
    this.router.navigate(['ledger', 'transactions', transactionId, 'reverse']);
  }

  protected applyFilter(): void {
    this.loadTransactions();
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe((rows: Instrument[]) => this.instruments.set(rows));
  }

  private loadTransactions(): void {
    const { accountId, from, to } = this.form.getRawValue();
    this.loadStatus.set('loading');
    this.ledger
      .listTransactions({ accountId, from, to })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: TransactionRow[]) => {
          this.transactions.set(rows);
          this.loadStatus.set('ready');
        },
        error: () => this.loadStatus.set('error')
      });
  }

  private initFilterForm(): void {
    this.form = this.fb.group({
      accountId: this.fb.nonNullable.control(''),
      from: this.fb.nonNullable.control(''),
      to: this.fb.nonNullable.control('')
    });
  }

  ngOnInit(): void {
    this.initFilterForm();
    this.loadInstruments();
    this.loadTransactions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
