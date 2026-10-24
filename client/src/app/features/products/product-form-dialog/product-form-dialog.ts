import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { applyServerErrors, errorMessage } from '../../../core/problem';
import { PRODUCT_TYPES, Product, ProductRequest, UNITS } from '../product.models';
import { ProductsApi } from '../products.api';

export interface ProductFormDialogData {
  /** The product to edit; omitted to create a new one. */
  product?: Product;
}

@Component({
  selector: 'app-product-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './product-form-dialog.html',
  styleUrl: './product-form-dialog.scss',
})
export class ProductFormDialog {
  private readonly api = inject(ProductsApi);
  private readonly dialogRef = inject<MatDialogRef<ProductFormDialog, Product>>(MatDialogRef);
  protected readonly product = inject<ProductFormDialogData>(MAT_DIALOG_DATA).product;

  protected readonly types = PRODUCT_TYPES;
  protected readonly units = UNITS;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  // Limits match the API (and the database columns).
  protected readonly form = inject(NonNullableFormBuilder).group({
    code: [this.product?.code ?? '', [Validators.required, Validators.maxLength(32)]],
    name: [this.product?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    type: [this.product?.type ?? PRODUCT_TYPES[0].value, Validators.required],
    unit: [this.product?.unit ?? UNITS[0].value, Validators.required],
    minStock: [this.product?.minStock ?? 0, [Validators.required, Validators.min(0)]],
    isActive: [this.product?.isActive ?? true],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const request: ProductRequest = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);

    const save$ = this.product
      ? this.api.update(this.product.id, request)
      : this.api.create(request);
    save$.subscribe({
      next: (product) => this.dialogRef.close(product),
      error: (error: unknown) => {
        this.saving.set(false);
        if (error instanceof HttpErrorResponse && error.status === 409) {
          // The only conflict here is a code that is already taken.
          this.form.controls.code.setErrors({ server: errorMessage(error) });
        } else if (!applyServerErrors(error, this.form)) {
          this.error.set(errorMessage(error));
        }
      },
    });
  }
}
