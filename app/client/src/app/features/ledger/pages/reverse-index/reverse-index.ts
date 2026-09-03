import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

type IdForm = FormGroup<{
  id: FormControl<string>;
}>;

@Component({
  selector: 'app-reverse-index',
  imports: [ReactiveFormsModule],
  templateUrl: './reverse-index.html',
  styleUrl: './reverse-index.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReverseIndex implements OnInit {
  protected form!: IdForm;

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly router: Router = inject(Router);

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const id: string = this.form.getRawValue().id.trim();
    if(id === '') {
      return;
    }
    this.router.navigate(['ledger', 'transactions', id, 'reverse']);
  }

  private initIdForm(): void {
    this.form = this.fb.group({
      id: this.fb.nonNullable.control('', { validators: Validators.required })
    });
  }

  ngOnInit(): void {
    this.initIdForm();
  }
}
