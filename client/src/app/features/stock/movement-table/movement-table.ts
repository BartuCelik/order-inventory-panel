import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { unitLabel } from '../../products/product.models';
import { StockMovement, movementTypeLabel } from '../stock.models';

/** Read-only ledger rows. The ledger is append-only, so there are no row actions. */
@Component({
  selector: 'app-movement-table',
  imports: [DatePipe, DecimalPipe, MatTableModule, RouterLink],
  template: `
    <table mat-table [dataSource]="movements()">
      <ng-container matColumnDef="createdAt">
        <th mat-header-cell *matHeaderCellDef>Date</th>
        <td mat-cell *matCellDef="let m" class="nowrap">
          {{ m.createdAt | date: 'dd MMM yyyy, HH:mm' }}
        </td>
      </ng-container>
      <ng-container matColumnDef="product">
        <th mat-header-cell *matHeaderCellDef>Product</th>
        <td mat-cell *matCellDef="let m">
          <a class="code" [routerLink]="['/products', m.productId]">{{ m.productCode }}</a>
          <span class="muted"> · {{ m.productName }}</span>
        </td>
      </ng-container>
      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>Type</th>
        <td mat-cell *matCellDef="let m">{{ movementTypeLabel(m.type) }}</td>
      </ng-container>
      <ng-container matColumnDef="quantity">
        <th mat-header-cell *matHeaderCellDef class="number">Quantity</th>
        <td
          mat-cell
          *matCellDef="let m"
          class="number"
          [class.in]="m.quantity > 0"
          [class.out]="m.quantity < 0"
        >
          {{ m.quantity > 0 ? '+' : '' }}{{ m.quantity | number: '1.0-4' }} {{ unitLabel(m.unit) }}
        </td>
      </ng-container>
      <ng-container matColumnDef="note">
        <th mat-header-cell *matHeaderCellDef>Note</th>
        <td mat-cell *matCellDef="let m" class="muted">{{ m.note }}</td>
      </ng-container>
      <ng-container matColumnDef="createdBy">
        <th mat-header-cell *matHeaderCellDef>By</th>
        <td mat-cell *matCellDef="let m" class="muted">{{ m.createdBy }}</td>
      </ng-container>

      <tr mat-header-row *matHeaderRowDef="columns()"></tr>
      <tr mat-row *matRowDef="let m; columns: columns()"></tr>
      <tr class="mat-mdc-row" *matNoDataRow>
        <td class="mat-mdc-cell muted empty" [attr.colspan]="columns().length">
          No stock movements yet.
        </td>
      </tr>
    </table>
  `,
  styles: `
    .code {
      font-family: 'Roboto Mono', monospace;
      color: var(--mat-sys-primary);
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .nowrap {
      white-space: nowrap;
    }
    .number {
      text-align: right;
      white-space: nowrap;
    }
    .in {
      color: #1b873f;
    }
    .out {
      color: var(--mat-sys-error);
    }
    .empty {
      padding: 24px 16px;
    }
  `,
})
export class MovementTable {
  readonly movements = input.required<StockMovement[]>();
  /** Hide the product column when every row belongs to the same product. */
  readonly showProduct = input(true);

  protected readonly movementTypeLabel = movementTypeLabel;
  protected readonly unitLabel = unitLabel;
  protected readonly columns = computed(() =>
    ['createdAt', 'product', 'type', 'quantity', 'note', 'createdBy'].filter(
      (c) => c !== 'product' || this.showProduct(),
    ),
  );
}
