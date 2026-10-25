import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, tap } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { Paged, unitLabel } from '../../products/product.models';
import {
  WORK_ORDER_STATUSES,
  WorkOrderStatus,
  WorkOrderSummary,
  isOpen,
  statusClass,
  statusLabel,
} from '../work-order.models';
import { TranslatePipe } from '../../../core/i18n/i18n';

/** The status filter: a single status, all open ones, or everything. */
type StatusFilter = WorkOrderStatus | 'open' | 'all';

@Component({
  selector: 'app-work-order-list',
  imports: [
    TranslatePipe,
    DatePipe,
    DecimalPipe,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './work-order-list.html',
  styleUrl: './work-order-list.scss',
})
export class WorkOrderList {
  private readonly router = inject(Router);

  protected readonly statuses = WORK_ORDER_STATUSES;
  protected readonly columns = ['status', 'workOrder', 'quantity', 'for', 'planned', 'flags'];
  protected readonly statusClass = statusClass;
  protected readonly statusLabel = statusLabel;
  protected readonly unitLabel = unitLabel;
  protected readonly isOpen = isOpen;
  protected readonly errorMessage = errorMessage;

  protected readonly search = signal('');
  protected readonly filter = signal<StatusFilter>(initialFilter(inject(ActivatedRoute)));
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);

  private readonly debouncedSearch = toSignal(
    toObservable(this.search).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.pageIndex.set(0)),
    ),
    { initialValue: '' },
  );

  protected readonly workOrders = httpResource<Paged<WorkOrderSummary>>(() => {
    const search = this.debouncedSearch().trim();
    const filter = this.filter();
    return {
      url: `${API_BASE_URL}/work-orders`,
      params: {
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        ...(search ? { search } : {}),
        ...(filter === 'open' ? { open: true } : filter === 'all' ? {} : { status: filter }),
      },
    };
  });

  protected readonly page = linkedSignal<
    Paged<WorkOrderSummary> | undefined,
    Paged<WorkOrderSummary> | undefined
  >({
    source: () => (this.workOrders.hasValue() ? this.workOrders.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

  protected setFilter(filter: StatusFilter): void {
    this.filter.set(filter);
    this.pageIndex.set(0);
  }

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected open(workOrder: WorkOrderSummary): void {
    this.router.navigate(['/work-orders', workOrder.id]);
  }
}

/** Links from the dashboard pass ?open=true or ?status=…; the default view is the open work. */
function initialFilter(route: ActivatedRoute): StatusFilter {
  const params = route.snapshot.queryParamMap;
  const status = params.get('status');
  if (WORK_ORDER_STATUSES.some((s) => s.value === status)) {
    return status as WorkOrderStatus;
  }
  return params.get('all') === 'true' ? 'all' : 'open';
}
