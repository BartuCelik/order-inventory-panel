import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { applyServerErrors, errorMessage } from '../../../core/problem';
import { Customer, CustomerRequest } from '../order.models';
import { OrdersApi } from '../orders.api';

export interface CustomerFormDialogData {
  /** The customer to edit; omitted to add a new one. */
  customer?: Customer;
}

@Component({
  selector: 'app-customer-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ customer ? 'Edit customer' : 'New customer' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="fields">
        <mat-form-field appearance="outline">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" cdkFocusInitial />
          @if (form.controls.name.hasError('required')) {
            <mat-error>Enter the customer's name.</mat-error>
          } @else if (form.controls.name.hasError('server')) {
            <mat-error>{{ form.controls.name.getError('server') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Contact person</mat-label>
          <input matInput formControlName="contactName" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" />
          @if (form.controls.email.hasError('email')) {
            <mat-error>Enter a valid email address.</mat-error>
          } @else if (form.controls.email.hasError('server')) {
            <mat-error>{{ form.controls.email.getError('server') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Phone</mat-label>
          <input matInput formControlName="phone" />
        </mat-form-field>
        @if (error(); as message) {
          <p class="form-error" role="alert">{{ message }}</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Close</button>
        <button mat-flat-button type="submit" [disabled]="saving()">
          {{ customer ? 'Save customer' : 'Add customer' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .fields {
      display: grid;
      gap: 4px;
      min-width: min(420px, 80vw);
      padding-top: 8px !important;
    }
    .form-error {
      margin: 0;
      color: var(--alarm);
      font-weight: 500;
    }
  `,
})
export class CustomerFormDialog {
  private readonly api = inject(OrdersApi);
  private readonly dialogRef = inject<MatDialogRef<CustomerFormDialog, Customer>>(MatDialogRef);
  protected readonly customer = inject<CustomerFormDialogData>(MAT_DIALOG_DATA).customer;

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  // Lengths match the API and the database columns.
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.customer?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    contactName: [this.customer?.contactName ?? '', Validators.maxLength(200)],
    email: [this.customer?.email ?? '', [Validators.email, Validators.maxLength(256)]],
    phone: [this.customer?.phone ?? '', Validators.maxLength(32)],
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const request: CustomerRequest = {
      name: value.name.trim(),
      contactName: value.contactName.trim() || null,
      email: value.email.trim() || null,
      phone: value.phone.trim() || null,
    };
    this.saving.set(true);
    this.error.set(null);
    const save$ = this.customer
      ? this.api.updateCustomer(this.customer.id, request)
      : this.api.createCustomer(request);
    save$.subscribe({
      next: (customer) => this.dialogRef.close(customer),
      error: (error: unknown) => {
        this.saving.set(false);
        if (!applyServerErrors(error, this.form)) {
          this.error.set(errorMessage(error));
        }
      },
    });
  }
}
