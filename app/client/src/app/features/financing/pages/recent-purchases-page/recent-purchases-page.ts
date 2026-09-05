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
import { RecentPurchaseRow } from '../../types/recent-purchase-row';
import { RecentPurchasesTable } from './recent-purchases-table';

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-recent-purchases-page',
  imports: [RecentPurchasesTable],
  templateUrl: './recent-purchases-page.html',
  styleUrl: './recent-purchases-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RecentPurchasesPage implements OnInit, OnDestroy {
  protected readonly purchases: WritableSignal<RecentPurchaseRow[]> = signal<RecentPurchaseRow[]>([]);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');

  private readonly financing: FinancingService = inject(FinancingService);
  private readonly destroy$: Subject<void> = new Subject<void>();

  private loadPurchases(): void {
    this.loadStatus.set('loading');
    this.financing
      .recentPurchases()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: RecentPurchaseRow[]) => {
          this.purchases.set(rows);
          this.loadStatus.set('ready');
        },
        error: () => this.loadStatus.set('error')
      }
    );
  }

  ngOnInit(): void {
    this.loadPurchases();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
