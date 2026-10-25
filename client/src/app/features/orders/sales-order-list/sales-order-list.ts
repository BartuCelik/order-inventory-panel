import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, tap } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { Paged } from '../../products/product.models';
import {
  SALES_ORDER_STATUSES,
  SalesOrderStatus,
  SalesOrderSummary,
  dueLabel,
  orderStatusClass,
} from '../order.models';

/** A single status, only the late ones, or everything. */
type OrderFilter = SalesOrderStatus | 'late' | 'all';

@Component({
  selector: 'app-sales-order-list',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './sales-order-list.html',
  styles: `
    .toolbar {
      padding-bottom: 10px;
    }
    .count {
      margin-left: auto;
      font-size: 14px;
    }
  `,
})
export class SalesOrderList {
  private readonly router = inject(Router);

  protected readonly statuses = SALES_ORDER_STATUSES;
  protected readonly columns = ['status', 'order', 'due', 'ordered'];
  protected readonly orderStatusClass = orderStatusClass;
  protected readonly dueLabel = dueLabel;
  protected readonly errorMessage = errorMessage;

  protected readonly search = signal('');
  protected readonly filter = signal<OrderFilter>(initialFilter(inject(ActivatedRoute)));
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

  protected readonly orders = httpResource<Paged<SalesOrderSummary>>(() => {
    const search = this.debouncedSearch().trim();
    const filter = this.filter();
    return {
      url: `${API_BASE_URL}/sales-orders`,
      params: {
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        ...(search ? { search } : {}),
        ...(filter === 'late' ? { late: true } : filter === 'all' ? {} : { status: filter }),
      },
    };
  });

  protected readonly page = linkedSignal<
    Paged<SalesOrderSummary> | undefined,
    Paged<SalesOrderSummary> | undefined
  >({
    source: () => (this.orders.hasValue() ? this.orders.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

  protected setFilter(filter: OrderFilter): void {
    this.filter.set(filter);
    this.pageIndex.set(0);
  }

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected open(order: SalesOrderSummary): void {
    this.router.navigate(['/sales-orders', order.id]);
  }
}

/** Links from the dashboard pass ?late=true or ?status=…; the default view is the open orders. */
function initialFilter(route: ActivatedRoute): OrderFilter {
  const params = route.snapshot.queryParamMap;
  if (params.get('late') === 'true') return 'late';
  const status = params.get('status');
  return SALES_ORDER_STATUSES.some((s) => s.value === status)
    ? (status as SalesOrderStatus)
    : 'Open';
}
