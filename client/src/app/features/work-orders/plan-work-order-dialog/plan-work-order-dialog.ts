import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { applyServerErrors, errorMessage } from '../../../core/problem';
import { isoDate } from '../../orders/order.models';
import { WorkOrder } from '../work-order.models';
import { WorkOrdersApi } from '../work-orders.api';

export interface PlanWorkOrderDialogData {
  productId: number;
  productName: string;
  unitLabel: string;
  /** Proposed quantity: what the line still needs beyond stock and existing work orders. */
  quantity: number;
  salesOrderLineId?: number;
  /** Shown under the title, e.g. "For Harbor Street Café, SO-00001". */
  context?: string;
}

@Component({
  selector: 'app-plan-work-order-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Plan production</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p class="what">
          <strong>{{ data.productName }}</strong>
          @if (data.context) {
            <br /><span class="muted">{{ data.context }}</span>
          }
        </p>
        <div class="fields">
          <mat-form-field appearance="outline">
            <mat-label>Quantity</mat-label>
            <!-- min 0, not 0.0001: the arrows step from min, so 0.0001 made 8 → 9.0001. "Above zero" is the validator's job. -->
            <input
              matInput
              type="number"
              min="0"
              step="1"
              formControlName="quantity"
              cdkFocusInitial
            />
            <span matTextSuffix>{{ data.unitLabel }}</span>
            @if (form.controls.quantity.hasError('server')) {
              <mat-error>{{ form.controls.quantity.getError('server') }}</mat-error>
            } @else if (form.controls.quantity.invalid) {
              <mat-error>Enter a quantity above zero.</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Planned for</mat-label>
            <input matInput type="date" formControlName="plannedDate" />
          </mat-form-field>
        </div>
        <p class="muted note">
          The components are copied from the current bill of materials and reserved for this work
          order.
        </p>
        @if (error(); as message) {
          <p class="form-error" role="alert">{{ message }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Close</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Create work order</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .what {
      margin: 0 0 12px;
    }
    .fields {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
      min-width: min(440px, 80vw);
    }
    .note {
      margin: 0;
      max-width: 52ch;
      font-size: 13.5px;
    }
    .form-error {
      margin: 8px 0 0;
      color: var(--alarm);
      font-weight: 500;
    }
  `,
})
export class PlanWorkOrderDialog {
  private readonly api = inject(WorkOrdersApi);
  private readonly dialogRef = inject<MatDialogRef<PlanWorkOrderDialog, WorkOrder>>(MatDialogRef);
  protected readonly data = inject<PlanWorkOrderDialogData>(MAT_DIALOG_DATA);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    quantity: [this.data.quantity, [Validators.required, Validators.min(0.0001)]],
    plannedDate: [isoDate(new Date()), Validators.required],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { quantity, plannedDate } = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .create({
        productId: this.data.productId,
        quantity,
        plannedDate,
        salesOrderLineId: this.data.salesOrderLineId,
      })
      .subscribe({
        next: (workOrder) => this.dialogRef.close(workOrder),
        error: (error: unknown) => {
          this.saving.set(false);
          if (!applyServerErrors(error, this.form)) {
            this.error.set(errorMessage(error));
          }
        },
      });
  }
}
