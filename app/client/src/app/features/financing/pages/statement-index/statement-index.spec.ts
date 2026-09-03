import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { StatementIndex } from './statement-index';

type IndexView = {
  form: FormGroup<{ id: FormControl<string> }>;
  onSubmit: () => void;
};

describe('StatementIndex', () => {
  let fixture: ComponentFixture<StatementIndex>;
  let view: IndexView;
  let navigate: jasmine.Spy<(commands: unknown[]) => Promise<boolean>>;

  beforeEach(() => {
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [StatementIndex],
      providers: [
        provideZonelessChangeDetection(),
        { provide: Router, useValue: { navigate } },
      ],
    });
    fixture = TestBed.createComponent(StatementIndex);
    view = fixture.componentInstance as unknown as IndexView;
    fixture.detectChanges();
  });

  it('creates', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('navigates to the id-driven route on submit, trimming the id', () => {
    view.form.setValue({ id: '  st-9  ' });
    view.onSubmit();
    expect(navigate).toHaveBeenCalledWith(['financing', 'statements', 'st-9']);
  });
  it('does not navigate when the id is blank', () => {
    view.onSubmit();
    expect(navigate).not.toHaveBeenCalled();
  });
});
