import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { LedgerService } from '../../ledger-service';
import { ReverseTransactionResult } from '../../types/reverse-transaction-result';

type ReverseStatus = 'idle' | 'reversing' | 'reversed' | 'error';

@Component({
  selector: 'app-reverse-movement-page',
  imports: [],
  templateUrl: './reverse-movement-page.html',
  styleUrl: './reverse-movement-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReverseMovementPage implements OnInit, OnDestroy {
  protected readonly transactionId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly status: WritableSignal<ReverseStatus> = signal<ReverseStatus>('idle');
  protected readonly result: WritableSignal<ReverseTransactionResult | null> =
    signal<ReverseTransactionResult | null>(null);
  protected readonly error: WritableSignal<AppError | null> = signal<AppError | null>(null);

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly ledger: LedgerService = inject(LedgerService);
  private readonly errorMessages: Record<string, string> = {
    'Ledger.AlreadyReversed': 'This transaction has already been reversed.',
    'Ledger.CannotReverseAReversal': 'A reversal cannot itself be reversed.',
    'Http.NotFound': 'No transaction matches that id.',
    'Http.Conflict': 'This transaction cannot be reversed in its current state.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.',
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
    return this.errorMessages[error.code] ?? 'The movement could not be reversed.';
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => this.transactionId.set(params.get('id')),
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
