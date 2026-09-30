import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideZonelessChangeDetection(), provideRouter(routes)]
    }).compileComponents();
  });
  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  describe('primary nav', () => {
    let nav: HTMLElement;
    const linkTexts = (group: Element): string[] =>
      Array.from(group.querySelectorAll('a')).map((a: HTMLAnchorElement) => a.textContent?.trim() ?? '');

    beforeEach(() => {
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      nav = fixture.nativeElement.querySelector('nav');
    });

    it('renders three groups in order Views / Setup / Actions', () => {
      const groups: Element[] = Array.from(nav.querySelectorAll('ul'));
      expect(groups.map((g) => g.getAttribute('aria-label'))).toEqual(['Views', 'Setup', 'Actions']);
    });
    it('lists the links of each group in the agreed order', () => {
      const [views, setup, actions]: Element[] = Array.from(nav.querySelectorAll('ul'));
      expect(linkTexts(views)).toEqual([
        'Dashboard',
        'Recent Money Movements',
        'Owed to Creditors',
        'Parties',
        'Subscriptions',
        'Credit Card Cycles'
      ]);
      expect(linkTexts(setup)).toEqual(['Cards and Accounts', 'Creditors']);
      expect(linkTexts(actions)).toEqual(['Load an Expense', 'Reverse a Transaction']);
    });
    it('marks only the action links with the action class', () => {
      const all: HTMLAnchorElement[] = Array.from(nav.querySelectorAll('a'));
      const action: HTMLAnchorElement[] = all.filter((a) => a.classList.contains('primary-nav__link--action'));
      expect(action.map((a) => a.textContent?.trim())).toEqual(['Load an Expense', 'Reverse a Transaction']);
    });
    it('no longer shows the retired labels', () => {
      const texts: string[] = Array.from(nav.querySelectorAll('a')).map((a) => a.textContent?.trim() ?? '');
      for (const old of ['Instruments', 'Statements', 'Reverse', 'Money Flow', 'Recent purchases']) {
        expect(texts).not.toContain(old);
      }
    });
  });
});
