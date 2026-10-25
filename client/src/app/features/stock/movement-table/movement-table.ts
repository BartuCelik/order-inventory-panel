import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { unitLabel } from '../../products/product.models';
import { StockMovement, movementTypeChip, movementTypeLabel } from '../stock.models';

/** Read-only ledger rows. The ledger is append-only, so there are no row actions. */
@Component({
  selector: 'app-movement-table',
  imports: [DatePipe, DecimalPipe, MatIconModule, MatTableModule, RouterLink],
  template: `
    <div class="table-scroll">
      <table mat-table class="data-table" [dataSource]="movements()">
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let m">
            <div class="stack">
              <span class="primary-line">{{ m.createdAt | date: 'd MMM yyyy' }}</span>
              <span class="secondary-line">{{ m.createdAt | date: 'HH:mm' }}</span>
            </div>
          </td>
        </ng-container>
        <ng-container matColumnDef="product">
          <th mat-header-cell *matHeaderCellDef>Product</th>
          <td mat-cell *matCellDef="let m">
            <a class="stack product" [routerLink]="['/products', m.productId]">
              <span class="primary-line">{{ m.productName }}</span>
              <span class="secondary-line mono">{{ m.productCode }}</span>
            </a>
          </td>
        </ng-container>
        <ng-container matColumnDef="type">
          <th mat-header-cell *matHeaderCellDef>Type</th>
          <td mat-cell *matCellDef="let m">
            <span class="chip" [class]="movementTypeChip(m.type)">{{
              movementTypeLabel(m.type)
            }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="quantity">
          <th mat-header-cell *matHeaderCellDef class="num">Quantity</th>
          <td
            mat-cell
            *matCellDef="let m"
            class="num qty"
            [class.in]="m.quantity > 0"
            [class.out]="m.quantity < 0"
          >
            {{ m.quantity > 0 ? '+' : '' }}{{ m.quantity | number: '1.0-4' }}
            <span class="muted">{{ unitLabel(m.unit) }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="note">
          <th mat-header-cell *matHeaderCellDef>Note</th>
          <td mat-cell *matCellDef="let m" class="muted note">{{ m.note ?? '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="createdBy">
          <th mat-header-cell *matHeaderCellDef>By</th>
          <td mat-cell *matCellDef="let m">
            <span class="who"
              ><span class="avatar">{{ m.createdBy.charAt(0).toUpperCase() }}</span
              >{{ m.createdBy }}</span
            >
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns()"></tr>
        <tr mat-row *matRowDef="let m; columns: columns()"></tr>
        <tr class="mat-mdc-row" *matNoDataRow>
          <td class="mat-mdc-cell" [attr.colspan]="columns().length">
            <div class="empty-state">
              <mat-icon>receipt_long</mat-icon>
              No stock movements yet.
            </div>
          </td>
        </tr>
      </table>
    </div>
  `,
  styles: `
    .qty {
      font-weight: 600;
    }
    .note {
      max-width: 280px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .product:hover {
      text-decoration: none;
    }
    .product:hover .primary-line {
      color: var(--mat-sys-primary);
    }
    .who {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: var(--text-2);
    }
    .avatar {
      display: grid;
      place-items: center;
      width: 24px;
      height: 24px;
      border-radius: 50%;
      background: var(--surface-muted);
      color: var(--text-2);
      font-size: 11px;
      font-weight: 600;
    }
  `,
})
export class MovementTable {
  readonly movements = input.required<StockMovement[]>();
  /** Hide the product column when every row belongs to the same product. */
  readonly showProduct = input(true);

  protected readonly movementTypeLabel = movementTypeLabel;
  protected readonly movementTypeChip = movementTypeChip;
  protected readonly unitLabel = unitLabel;
  protected readonly columns = computed(() =>
    ['createdAt', 'product', 'type', 'quantity', 'note', 'createdBy'].filter(
      (c) => c !== 'product' || this.showProduct(),
    ),
  );
}
