import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, ParamMap, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { formatMoney } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../../reports/reports-service';
import { TransactionFeedRow } from '../../../reports/types/transaction-feed-row';
import { LedgerService } from '../../ledger-service';
import { ReverseTransactionResult } from '../../types/reverse-transaction-result';

type LoadStatus = 'loading' | 'ready' | 'notFound' | 'error';
type ReverseStatus = 'idle' | 'reversing' | 'reversed' | 'error';

@Component({
  selector: 'app-reverse-movement-page',
  imports: [RouterLink],
  templateUrl: './reverse-movement-page.html',
  styleUrl: './reverse-movement-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReverseMovementPage implements OnInit, OnDestroy {
  protected readonly transactionId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly row: WritableSignal<TransactionFeedRow | null> = signal<TransactionFeedRow | null>(null);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;
  protected readonly status: WritableSignal<ReverseStatus> = signal<ReverseStatus>('idle');
  protected readonly result: WritableSignal<ReverseTransactionResult | null> =
    signal<ReverseTransactionResult | null>(null);
  protected readonly error: WritableSignal<AppError | null> = signal<AppError | null>(null);

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly ledger: LedgerService = inject(LedgerService);
  private readonly reports: ReportsService = inject(ReportsService);
  private readonly errorMessages: Record<string, string> = {
    'Ledger.TransactionAlreadyReversed': 'This transaction was already undone.',
    'Ledger.CannotReverseAReversal': "An undo entry can't be undone again.",
    'Http.NotFound': "We couldn't find that transaction.",
    'Http.Conflict': "This transaction can't be undone in its current state.",
    'Http.ServerError': 'Something went wrong. Try again in a moment.',
    'Http.NetworkError': "We couldn't connect. Check your connection and try again.",
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onConfirm(): void {
    const id: string | null = this.transactionId();
    if(id === null || this.status() === 'reversing') {
      return;
    }
    this.error.set(null);
    this.status.set('reversing');
    this.ledger
      .reverse(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: ReverseTransactionResult) => {
          this.result.set(result);
          this.status.set('reversed');
        },
        error: (error: AppError) => {
          this.error.set(error);
          this.status.set('error');
        }
      }
    );
  }

  protected errorText(error: AppError): string {
    return this.errorMessages[error.code] ?? "The transaction couldn't be undone.";
  }

  private loadRow(id: string): void {
    this.loadStatus.set('loading');
    this.reports
      .transaction(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (row: TransactionFeedRow) => {
          this.row.set(row);
          this.loadStatus.set('ready');
        },
        error: (error: AppError) => this.loadStatus.set(error.status === 404 ? 'notFound' : 'error')
      }
    );
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => {
        const id: string | null = params.get('id');
        this.transactionId.set(id);
        if(id !== null) {
          this.loadRow(id);
        }
      },
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
