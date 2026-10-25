import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { confirm } from '../../../shared/confirm-dialog/confirm-dialog';
import { unitLabel } from '../../products/product.models';
import {
  PlanWorkOrderDialog,
  PlanWorkOrderDialogData,
} from '../../work-orders/plan-work-order-dialog/plan-work-order-dialog';
import { WorkOrder, isOpen, statusClass, statusLabel } from '../../work-orders/work-order.models';
import {
  SalesOrder,
  SalesOrderLine,
  dueLabel,
  orderStatusClass,
  orderStatusLabel,
} from '../order.models';
import { OrdersApi } from '../orders.api';
import { AuthService } from '../../../core/auth/auth.service';
import { t, TranslatePipe } from '../../../core/i18n/i18n';

@Component({
  selector: 'app-sales-order-detail',
  imports: [
    TranslatePipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './sales-order-detail.html',
  styleUrl: './sales-order-detail.scss',
})
export class SalesOrderDetail {
  private readonly api = inject(OrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  /** Route parameter, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly unitLabel = unitLabel;
  protected readonly dueLabel = dueLabel;
  protected readonly orderStatusClass = orderStatusClass;
  protected readonly orderStatusLabel = orderStatusLabel;
  protected readonly statusClass = statusClass;
  protected readonly statusLabel = statusLabel;
  protected readonly isOpen = isOpen;
  protected readonly errorMessage = errorMessage;

  protected readonly order = httpResource<SalesOrder>(
    () => `${API_BASE_URL}/sales-orders/${this.id()}`,
  );

  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly columns = computed(() =>
    this.order.hasValue() && this.order.value().status === 'Open'
      ? ['product', 'quantity', 'onHand', 'production', 'plan']
      : ['product', 'quantity', 'production'],
  );

  /** Lines whose finished goods are not all on the shelf: shipping would be rejected (rule 14). */
  protected readonly shortLines = computed(() =>
    this.order.hasValue() ? this.order.value().lines.filter((l) => l.onHand < l.quantity) : [],
  );

  protected readonly openWorkOrderCount = computed(() =>
    this.order.hasValue()
      ? this.order
          .value()
          .lines.flatMap((l) => l.workOrders)
          .filter((w) => isOpen(w.status)).length
      : 0,
  );

  /** What the line still needs beyond stock and the work orders already planned or done for it. */
  protected stillNeeded(line: SalesOrderLine): number {
    const covered = line.workOrders
      .filter((w) => w.status !== 'Cancelled')
      .reduce((sum, w) => sum + w.quantity, 0);
    return Math.max(0, line.quantity - line.onHand - covered);
  }

  protected plan(order: SalesOrder, line: SalesOrderLine): void {
    this.dialog
      .open<PlanWorkOrderDialog, PlanWorkOrderDialogData, WorkOrder>(PlanWorkOrderDialog, {
        data: {
          productId: line.productId,
          productName: line.productName,
          unitLabel: unitLabel(line.unit),
          quantity: this.stillNeeded(line) || line.quantity,
          salesOrderLineId: line.id,
          context: t('salesOrder.planContext', {
            customer: order.customerName,
            number: order.number,
          }),
        },
      })
      .afterClosed()
      .subscribe((workOrder) => {
        if (workOrder) {
          this.snackBar
            .open(
              t('salesOrder.workOrderCreated', { number: workOrder.number }),
              t('common.open'),
              {
                duration: 5000,
              },
            )
            .onAction()
            .subscribe(() => this.router.navigate(['/work-orders', workOrder.id]));
          this.order.reload();
        }
      });
  }

  protected ship(order: SalesOrder): void {
    confirm(this.dialog, {
      title: t('salesOrder.shipTitle', { number: order.number }),
      message: t('salesOrder.shipMessage'),
      confirmLabel: t('salesOrder.shipConfirm'),
    })
      .pipe(filter(Boolean))
      .subscribe(() =>
        this.run(this.api.ship(order.id), t('salesOrder.shippedDone', { number: order.number })),
      );
  }

  protected cancel(order: SalesOrder): void {
    const open = this.openWorkOrderCount();
    confirm(this.dialog, {
      title: t('salesOrder.cancelTitle', { number: order.number }),
      message:
        open > 0 ? t('salesOrder.cancelWithWork', { count: open }) : t('salesOrder.cancelPlain'),
      confirmLabel: t('salesOrder.cancelOrder'),
    })
      .pipe(filter(Boolean))
      .subscribe(() =>
        this.run(
          this.api.cancel(order.id),
          t('salesOrder.cancelledDone', { number: order.number }),
        ),
      );
  }

  private run(action$: ReturnType<OrdersApi['ship']>, done: string): void {
    this.busy.set(true);
    this.actionError.set(null);
    action$.subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busy.set(false);
        this.snackBar.open(done, undefined, { duration: 3000 });
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.actionError.set(errorMessage(error));
        this.order.reload();
      },
    });
  }
}
