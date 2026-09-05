import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { FinancingService } from '../../financing-service';
import { CreditorPayableRow } from '../../types/creditor-payable-row';
import { CreditorPayablesTable } from './creditor-payables-table';

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-creditor-payables-page',
  imports: [CreditorPayablesTable],
  templateUrl: './creditor-payables-page.html',
  styleUrl: './creditor-payables-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreditorPayablesPage implements OnInit, OnDestroy {
  protected readonly payables: WritableSignal<CreditorPayableRow[]> = signal<CreditorPayableRow[]>([]);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');

  private readonly financing: FinancingService = inject(FinancingService);
  private readonly destroy$: Subject<void> = new Subject<void>();

  private loadPayables(): void {
    this.loadStatus.set('loading');
    this.financing
      .creditorPayables()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: CreditorPayableRow[]) => {
          this.payables.set(rows);
          this.loadStatus.set('ready');
        },
        error: () => this.loadStatus.set('error')
      }
    );
  }

  ngOnInit(): void {
    this.loadPayables();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
