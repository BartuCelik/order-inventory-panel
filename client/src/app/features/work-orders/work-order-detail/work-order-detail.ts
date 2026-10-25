import { DatePipe, DecimalPipe, formatNumber } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { confirm } from '../../../shared/confirm-dialog/confirm-dialog';
import { typeChip, typeLabel, unitLabel } from '../../products/product.models';
import {
  WorkOrder,
  WorkOrderComponent,
  WorkOrderStatus,
  isOpen,
  statusClass,
  statusLabel,
} from '../work-order.models';
import { WorkOrderAction, WorkOrdersApi } from '../work-orders.api';
import { AuthService } from '../../../core/auth/auth.service';
import { TranslatePipe, currentLanguage, t } from '../../../core/i18n/i18n';

/** The normal route through a work order (rule 12); Cancelled leaves it from either open step. */
const STEPS: { status: WorkOrderStatus; label: string }[] = [
  { status: 'Planned', label: t('workStatus.Planned') },
  { status: 'InProgress', label: t('workStatus.InProgress') },
  { status: 'Done', label: t('workStatus.Done') },
];

@Component({
  selector: 'app-work-order-detail',
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
  templateUrl: './work-order-detail.html',
  styleUrl: './work-order-detail.scss',
})
export class WorkOrderDetail {
  private readonly api = inject(WorkOrdersApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly auth = inject(AuthService);

  /** Route parameter, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly steps = STEPS;
  protected readonly statusClass = statusClass;
  protected readonly statusLabel = statusLabel;
  protected readonly typeChip = typeChip;
  protected readonly typeLabel = typeLabel;
  protected readonly unitLabel = unitLabel;
  protected readonly isOpen = isOpen;
  protected readonly errorMessage = errorMessage;

  protected readonly workOrder = httpResource<WorkOrder>(
    () => `${API_BASE_URL}/work-orders/${this.id()}`,
  );

  protected readonly busy = signal(false);
  /** The last failed action's message, shown in place until the next action. */
  protected readonly actionError = signal<string | null>(null);

  protected readonly columns = computed(() =>
    this.workOrder.hasValue() && this.workOrder.value().status === 'Done'
      ? ['component', 'required', 'consumed', 'onHand']
      : ['component', 'required', 'onHand', 'reserved', 'available', 'level'],
  );

  /** Components that cannot be covered from physical stock: completing would be rejected (rule 10). */
  protected readonly physicallyShort = computed(() =>
    this.workOrder.hasValue()
      ? this.workOrder.value().components.filter((c) => c.onHand < c.requiredQty - c.consumedQty)
      : [],
  );

  protected stepState(
    step: WorkOrderStatus,
    current: WorkOrderStatus,
  ): 'done' | 'current' | 'todo' {
    const order: WorkOrderStatus[] = ['Planned', 'InProgress', 'Done'];
    if (current === 'Cancelled') return 'todo';
    const diff = order.indexOf(step) - order.indexOf(current);
    return diff < 0 ? 'done' : diff === 0 ? 'current' : 'todo';
  }

  /** Share of the requirement that is available to this work order, 0–100. */
  protected coverage(c: WorkOrderComponent): number {
    if (c.requiredQty <= 0) return 100;
    return Math.max(0, Math.min(100, (c.available / c.requiredQty) * 100));
  }

  protected start(w: WorkOrder): void {
    this.run(w, 'start', t('workOrder.started', { number: w.number }));
  }

  protected complete(w: WorkOrder): void {
    confirm(this.dialog, {
      title: t('workOrder.completeTitle', { number: w.number }),
      message: t('workOrder.completeMessage', {
        quantity: formatNumber(w.quantity, currentLanguage(), '1.0-4'),
        product: w.productName,
      }),
      confirmLabel: t('workOrder.complete'),
    })
      .pipe(filter(Boolean))
      .subscribe(() => this.run(w, 'complete', t('workOrder.completed', { number: w.number })));
  }

  protected cancel(w: WorkOrder): void {
    confirm(this.dialog, {
      title: t('workOrder.cancelTitle', { number: w.number }),
      message: t('workOrder.cancelMessage'),
      confirmLabel: t('workOrder.cancelConfirm'),
    })
      .pipe(filter(Boolean))
      .subscribe(() => this.run(w, 'cancel', t('workOrder.cancelled', { number: w.number })));
  }

  private run(w: WorkOrder, action: WorkOrderAction, done: string): void {
    this.busy.set(true);
    this.actionError.set(null);
    this.api.act(w.id, action).subscribe({
      next: (updated) => {
        this.workOrder.set(updated);
        this.busy.set(false);
        this.snackBar.open(done, undefined, { duration: 3000 });
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.actionError.set(errorMessage(error));
        // The status may have changed under us (409); show the current state.
        this.workOrder.reload();
      },
    });
  }
}
