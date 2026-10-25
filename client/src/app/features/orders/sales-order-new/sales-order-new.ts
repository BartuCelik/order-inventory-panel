import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { ProductPicker } from '../../products/product-picker/product-picker';
import { Paged, Product, unitLabel } from '../../products/product.models';
import {
  CustomerFormDialog,
  CustomerFormDialogData,
} from '../customer-form-dialog/customer-form-dialog';
import { Customer, isoDate } from '../order.models';
import { OrdersApi } from '../orders.api';
import { t, TranslatePipe } from '../../../core/i18n/i18n';

interface DraftLine {
  key: number;
  product: Product | null;
  quantity: number | null;
}

@Component({
  selector: 'app-sales-order-new',
  imports: [
    TranslatePipe,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    ProductPicker,
  ],
  templateUrl: './sales-order-new.html',
  styleUrl: './sales-order-new.scss',
})
export class SalesOrderNew {
  private readonly api = inject(OrdersApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  protected readonly unitLabel = unitLabel;
  protected readonly today = isoDate(new Date());

  protected readonly customers = httpResource<Paged<Customer>>(() => ({
    url: `${API_BASE_URL}/customers`,
    params: { pageSize: 100 },
  }));

  protected readonly customerId = signal<number | null>(null);
  protected readonly orderDate = signal(this.today);
  protected readonly dueDate = signal(isoDate(new Date(Date.now() + 14 * 86_400_000)));
  protected readonly lines = signal<DraftLine[]>([{ key: 1, product: null, quantity: 1 }]);
  private nextKey = 2;

  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Each product once per order, as the API requires. */
  protected readonly duplicateIds = computed(() => {
    const ids = this.lines()
      .map((l) => l.product?.id)
      .filter((id): id is number => id != null);
    return ids.filter((id, i) => ids.indexOf(id) !== i);
  });

  protected readonly problems = computed(() => {
    const problems: string[] = [];
    if (this.customerId() == null) problems.push(t('salesOrderNew.chooseCustomer'));
    if (!this.dueDate()) problems.push(t('salesOrderNew.enterDueDate'));
    else if (this.orderDate() && this.dueDate() < this.orderDate())
      problems.push(t('salesOrderNew.dueBeforeOrder'));
    if (this.lines().some((l) => !l.product)) problems.push(t('salesOrderNew.pickProduct'));
    if (this.lines().some((l) => !l.quantity || l.quantity <= 0))
      problems.push(t('salesOrderNew.quantityAboveZero'));
    if (this.duplicateIds().length > 0) problems.push(t('salesOrderNew.productOnce'));
    return problems;
  });

  protected addLine(): void {
    this.lines.update((lines) => [...lines, { key: this.nextKey++, product: null, quantity: 1 }]);
  }

  protected removeLine(key: number): void {
    this.lines.update((lines) => lines.filter((l) => l.key !== key));
  }

  protected setProduct(key: number, product: Product | null): void {
    this.lines.update((lines) => lines.map((l) => (l.key === key ? { ...l, product } : l)));
  }

  protected setQuantity(key: number, quantity: number | null): void {
    this.lines.update((lines) => lines.map((l) => (l.key === key ? { ...l, quantity } : l)));
  }

  protected newCustomer(): void {
    this.dialog
      .open<CustomerFormDialog, CustomerFormDialogData, Customer>(CustomerFormDialog, { data: {} })
      .afterClosed()
      .subscribe((customer) => {
        if (customer) {
          this.customers.reload();
          this.customerId.set(customer.id);
        }
      });
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.problems().length > 0) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api
      .createSalesOrder({
        customerId: this.customerId()!,
        orderDate: this.orderDate() || undefined,
        dueDate: this.dueDate(),
        lines: this.lines().map((l) => ({ productId: l.product!.id, quantity: l.quantity! })),
      })
      .subscribe({
        next: (order) => this.router.navigate(['/sales-orders', order.id]),
        error: (error: unknown) => {
          this.saving.set(false);
          this.error.set(errorMessage(error));
        },
      });
  }
}
