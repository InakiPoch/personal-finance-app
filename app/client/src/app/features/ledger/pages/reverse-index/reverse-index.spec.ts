import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { ReverseIndex } from './reverse-index';

type IndexView = {
  form: FormGroup<{ id: FormControl<string> }>;
  onSubmit: () => void;
};

describe('ReverseIndex', () => {
  let fixture: ComponentFixture<ReverseIndex>;
  let view: IndexView;
  let navigate: jasmine.Spy<(commands: unknown[]) => Promise<boolean>>;

  beforeEach(() => {
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [ReverseIndex],
      providers: [
        provideZonelessChangeDetection(),
        { provide: Router, useValue: { navigate } },
      ],
    });
    fixture = TestBed.createComponent(ReverseIndex);
    view = fixture.componentInstance as unknown as IndexView;
    fixture.detectChanges();
  });

  it('creates', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('navigates to the id-driven reverse route on submit, trimming the id', () => {
    view.form.setValue({ id: '  tx-9  ' });
    view.onSubmit();
    expect(navigate).toHaveBeenCalledWith(['ledger', 'transactions', 'tx-9', 'reverse']);
  });
  it('does not navigate when the id is blank', () => {
    view.onSubmit();
    expect(navigate).not.toHaveBeenCalled();
  });
});
