import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

type IdForm = FormGroup<{
  id: FormControl<string>;
}>;

@Component({
  selector: 'app-statement-index',
  imports: [ReactiveFormsModule],
  templateUrl: './statement-index.html',
  styleUrl: './statement-index.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatementIndex implements OnInit {
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
    this.router.navigate(['financing', 'statements', id]);
  }

  private initIdForm(): void {
    this.form = this.fb.group({
      id: this.fb.nonNullable.control('', { validators: Validators.required }),
    });
  }

  ngOnInit(): void {
    this.initIdForm();
  }
}
