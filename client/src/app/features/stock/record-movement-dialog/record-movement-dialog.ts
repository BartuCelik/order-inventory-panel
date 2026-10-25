import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { applyServerErrors, errorMessage } from '../../../core/problem';
import { ProductPicker } from '../../products/product-picker/product-picker';
import { Product, unitLabel } from '../../products/product.models';
import { StockApi } from '../stock.api';
import { MANUAL_MOVEMENT_TYPES, ManualMovementType, StockMovement } from '../stock.models';
import { t, TranslatePipe } from '../../../core/i18n/i18n';

export interface RecordMovementDialogData {
  /** Fixes the product, e.g. when opened from its detail page; otherwise the user picks one. */
  product?: Product;
}

@Component({
  selector: 'app-record-movement-dialog',
  imports: [
    TranslatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ProductPicker,
  ],
  templateUrl: './record-movement-dialog.html',
  styleUrl: './record-movement-dialog.scss',
})
export class RecordMovementDialog {
  private readonly api = inject(StockApi);
  private readonly dialogRef =
    inject<MatDialogRef<RecordMovementDialog, StockMovement>>(MatDialogRef);
  protected readonly fixedProduct = inject<RecordMovementDialogData>(MAT_DIALOG_DATA).product;

  protected readonly types = MANUAL_MOVEMENT_TYPES;
  protected readonly unitLabel = unitLabel;
  protected readonly product = signal<Product | null>(this.fixedProduct ?? null);
  protected readonly productError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      type: ['Receipt' as ManualMovementType, Validators.required],
      quantity: [null as number | null, Validators.required],
      note: ['', Validators.maxLength(500)],
    },
    { validators: quantityMatchesType },
  );

  private readonly type = toSignal(this.form.controls.type.valueChanges, {
    initialValue: this.form.controls.type.value,
  });
  protected readonly typeHint = computed(
    () => this.types.find((t) => t.value === this.type())?.hint ?? '',
  );

  protected save(): void {
    const product = this.product();
    this.productError.set(product ? null : t('record.pickProduct'));
    if (!product || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { type, quantity, note } = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .record({ productId: product.id, type, quantity: quantity!, note: note.trim() || null })
      .subscribe({
        next: (movement) => this.dialogRef.close(movement),
        error: (error: unknown) => {
          this.saving.set(false);
          // A 422 here is "not enough stock" (rule 3); its detail names the product and the shortfall.
          if (!applyServerErrors(error, this.form)) {
            this.error.set(errorMessage(error));
          }
        },
      });
  }
}

/** Receipt and Issue take a positive quantity; Adjustment any non-zero one. The API checks the same. */
function quantityMatchesType(group: AbstractControl): ValidationErrors | null {
  const type = group.get('type')?.value as ManualMovementType;
  const quantity = group.get('quantity')?.value as number | null;
  if (quantity === null) {
    return null;
  }
  if (type === 'Adjustment' ? quantity === 0 : quantity <= 0) {
    return { quantityForType: true };
  }
  return null;
}
