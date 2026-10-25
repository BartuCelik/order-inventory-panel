import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, tap } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { Paged } from '../../products/product.models';
import {
  CustomerFormDialog,
  CustomerFormDialogData,
} from '../customer-form-dialog/customer-form-dialog';
import { Customer } from '../order.models';
import { AuthService } from '../../../core/auth/auth.service';
import { TranslatePipe } from '../../../core/i18n/i18n';

@Component({
  selector: 'app-customer-list',
  imports: [
    TranslatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1 class="page-title">{{ 'nav.customers' | t }}</h1>
          <p class="page-subtitle">{{ 'customers.subtitle' | t }}</p>
        </div>
        @if (auth.canPlan()) {
          <div class="page-actions">
            <a mat-stroked-button routerLink="/sales-orders/new">{{ 'salesOrders.new' | t }}</a>
            <button mat-flat-button (click)="edit()">
              <mat-icon>add</mat-icon>
              {{ 'customers.new' | t }}
            </button>
          </div>
        }
      </header>

      <section class="panel">
        <div class="toolbar">
          <mat-form-field appearance="outline" class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <input
              matInput
              [placeholder]="'customers.searchPlaceholder' | t"
              [attr.aria-label]="'customers.searchLabel' | t"
              [value]="search()"
              (input)="search.set($any($event.target).value)"
            />
          </mat-form-field>
        </div>
        <div class="panel-progress">
          @if (customers.isLoading()) {
            <mat-progress-bar mode="indeterminate" />
          }
        </div>

        @if (customers.error(); as error) {
          <div class="empty-state error">
            <mat-icon>error</mat-icon>
            {{ errorMessage(error) }}
          </div>
        } @else if (page(); as page) {
          @if (page.items.length === 0) {
            <div class="empty-state">
              <mat-icon>storefront</mat-icon>
              {{ 'customers.noneMatch' | t }}
            </div>
          } @else {
            <div class="table-scroll">
              <table mat-table class="data-table" [dataSource]="page.items">
                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>{{ 'salesOrderNew.customer' | t }}</th>
                  <td mat-cell *matCellDef="let c">
                    <strong>{{ c.name }}</strong>
                  </td>
                </ng-container>
                <ng-container matColumnDef="contact">
                  <th mat-header-cell *matHeaderCellDef>{{ 'customers.contactPerson' | t }}</th>
                  <td mat-cell *matCellDef="let c">{{ c.contactName ?? '—' }}</td>
                </ng-container>
                <ng-container matColumnDef="email">
                  <th mat-header-cell *matHeaderCellDef>{{ 'customers.email' | t }}</th>
                  <td mat-cell *matCellDef="let c">
                    @if (c.email) {
                      <a [href]="'mailto:' + c.email" (click)="$event.stopPropagation()">{{
                        c.email
                      }}</a>
                    } @else {
                      —
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="phone">
                  <th mat-header-cell *matHeaderCellDef>{{ 'customers.phone' | t }}</th>
                  <td mat-cell *matCellDef="let c">{{ c.phone ?? '—' }}</td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr
                  mat-row
                  *matRowDef="let c; columns: columns"
                  [class.clickable]="auth.canPlan()"
                  [attr.tabindex]="auth.canPlan() ? 0 : null"
                  (click)="auth.canPlan() && edit(c)"
                  (keydown.enter)="auth.canPlan() && edit(c)"
                ></tr>
              </table>
            </div>
            <mat-paginator
              [length]="page.totalCount"
              [pageIndex]="pageIndex()"
              [pageSize]="pageSize()"
              [pageSizeOptions]="[10, 20, 50, 100]"
              (page)="changePage($event)"
              [attr.aria-label]="'customers.pages' | t"
            />
          }
        }
      </section>
    </div>
  `,
  styles: `
    .toolbar {
      padding-bottom: 10px;
    }
  `,
})
export class CustomerList {
  private readonly dialog = inject(MatDialog);
  protected readonly auth = inject(AuthService);

  protected readonly columns = ['name', 'contact', 'email', 'phone'];
  protected readonly errorMessage = errorMessage;
  protected readonly search = signal('');
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

  protected readonly customers = httpResource<Paged<Customer>>(() => {
    const search = this.debouncedSearch().trim();
    return {
      url: `${API_BASE_URL}/customers`,
      params: {
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        ...(search ? { search } : {}),
      },
    };
  });

  protected readonly page = linkedSignal<Paged<Customer> | undefined, Paged<Customer> | undefined>({
    source: () => (this.customers.hasValue() ? this.customers.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected edit(customer?: Customer): void {
    this.dialog
      .open<CustomerFormDialog, CustomerFormDialogData, Customer>(CustomerFormDialog, {
        data: { customer },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.customers.reload();
        }
      });
  }
}
