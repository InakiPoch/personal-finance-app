import { ChangeDetectionStrategy, Component, InputSignal, ModelSignal, input, model } from '@angular/core';

@Component({
  selector: 'app-expandable-form',
  templateUrl: './expandable-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExpandableForm {
  readonly formId: InputSignal<string> = input.required<string>();
  readonly title: InputSignal<string> = input.required<string>();
  readonly description: InputSignal<string> = input.required<string>();
  readonly open: ModelSignal<boolean> = model(false);

  protected toggle(): void {
    this.open.update((value: boolean) => !value);
  }
}
