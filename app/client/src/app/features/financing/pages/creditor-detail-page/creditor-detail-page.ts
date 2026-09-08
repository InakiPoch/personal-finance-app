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
import { FinancingService } from '../../financing-service';
import { CreditorDetail } from '../../types/creditor-detail';
import { CreditorPurchasesTable } from './creditor-purchases-table';

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-creditor-detail-page',
  imports: [CreditorPurchasesTable],
  templateUrl: './creditor-detail-page.html',
  styleUrl: './creditor-detail-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditorDetailPage implements OnInit, OnDestroy {
  protected readonly detail: WritableSignal<CreditorDetail | null> =
    signal<CreditorDetail | null>(null);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('idle');
  protected readonly loadError: WritableSignal<AppError | null> = signal<AppError | null>(null);

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected isNotFound(): boolean {
    return this.loadError()?.code === 'Financing.CreditorNotFound';
  }

  private loadDetail(creditorId: string): void {
    this.loadStatus.set('loading');
    this.loadError.set(null);
    this.financing
      .creditorDetail(creditorId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (detail: CreditorDetail) => {
          this.detail.set(detail);
          this.loadStatus.set('ready');
        },
        error: (error: AppError) => {
          this.loadError.set(error);
          this.loadStatus.set('error');
        }
      }
    );
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => {
        const id: string | null = params.get('creditorId');
        if(id !== null) {
          this.loadDetail(id);
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
