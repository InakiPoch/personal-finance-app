import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../financing-service';
import { RecentPurchaseRow } from '../../types/recent-purchase-row';
import { RecentPurchasesPage } from './recent-purchases-page';

type RecentPurchasesView = {
  purchases: () => RecentPurchaseRow[];
  loadStatus: () => 'idle' | 'loading' | 'ready' | 'error';
};

describe('RecentPurchasesPage', () => {
  let fixture: ComponentFixture<RecentPurchasesPage>;
  let view: RecentPurchasesView;
  let recentPurchases: jasmine.Spy<() => Observable<RecentPurchaseRow[]>>;

  const money = (value: number): Money => value as Money;

  const rows: RecentPurchaseRow[] = [{
    planId: 'plan-1',
    description: 'New laptop',
    cardName: 'Visa',
    purchaseDate: '2026-09-01',
    totalMinorUnits: money(1200000),
    installmentCount: 3,
    isCreditorPayment: false
  }];

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [RecentPurchasesPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { recentPurchases } }
      ]
    });
    fixture = TestBed.createComponent(RecentPurchasesPage);
    view = fixture.componentInstance as unknown as RecentPurchasesView;
    fixture.detectChanges();
  }

  it('fetches and renders the purchases on init', () => {
    recentPurchases = jasmine.createSpy('recentPurchases').and.returnValue(of(rows));
    setup();
    expect(recentPurchases).toHaveBeenCalled();
    expect(view.loadStatus()).toBe('ready');
    expect(view.purchases()).toEqual(rows);
  });
  it('renders the empty state when there are no purchases', () => {
    recentPurchases = jasmine.createSpy('recentPurchases').and.returnValue(of([]));
    setup();
    expect(view.loadStatus()).toBe('ready');
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('No purchases loaded yet.');
  });
  it('surfaces a load error without throwing', () => {
    recentPurchases = jasmine.createSpy('recentPurchases').and.returnValue(throwError(() => new Error('boom')));
    setup();
    expect(view.loadStatus()).toBe('error');
    expect(view.purchases()).toEqual([]);
  });
});
