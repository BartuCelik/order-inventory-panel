import { httpResource } from '@angular/common/http';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { errorMessage } from '../../../core/problem';
import { ProductPicker } from '../../products/product-picker/product-picker';
import { Paged, Product } from '../../products/product.models';
import { MovementTable } from '../movement-table/movement-table';
import {
  RecordMovementDialog,
  RecordMovementDialogData,
} from '../record-movement-dialog/record-movement-dialog';
import { STOCK_MOVEMENTS_URL } from '../stock.api';
import { StockMovement, movementTypeLabel } from '../stock.models';
import { AuthService } from '../../../core/auth/auth.service';
import { t, TranslatePipe } from '../../../core/i18n/i18n';
import { ExportButton } from '../../../shared/export-button/export-button';

@Component({
  selector: 'app-stock-ledger',
  imports: [
    ExportButton,
    TranslatePipe,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MovementTable,
    ProductPicker,
  ],
  templateUrl: './stock-ledger.html',
  styleUrl: './stock-ledger.scss',
})
export class StockLedger {
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly auth = inject(AuthService);

  protected readonly errorMessage = errorMessage;
  protected readonly pageSizeOptions = [20, 50, 100];

  protected readonly product = signal<Product | null>(null);
  protected readonly pageIndex = linkedSignal({ source: this.product, computation: () => 0 });
  protected readonly pageSize = signal(20);

  protected readonly url = STOCK_MOVEMENTS_URL;

  /** The table's filters without paging; the Excel export uses the same ones. */
  protected readonly filters = computed((): Record<string, number> => {
    const product = this.product();
    return product ? { productId: product.id } : {};
  });

  protected readonly movements = httpResource<Paged<StockMovement>>(() => ({
    url: this.url,
    params: { page: this.pageIndex() + 1, pageSize: this.pageSize(), ...this.filters() },
  }));

  /** The last loaded page, kept while the next one loads so the table does not flash empty. */
  protected readonly page = linkedSignal<
    Paged<StockMovement> | undefined,
    Paged<StockMovement> | undefined
  >({
    source: () => (this.movements.hasValue() ? this.movements.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected record(): void {
    this.dialog
      .open<RecordMovementDialog, RecordMovementDialogData, StockMovement>(RecordMovementDialog, {
        data: {},
      })
      .afterClosed()
      .subscribe((movement) => {
        if (movement) {
          this.snackBar.open(
            t('stock.recordedFor', {
              type: movementTypeLabel(movement.type),
              code: movement.productCode,
            }),
            undefined,
            {
              duration: 3000,
            },
          );
          this.movements.reload();
        }
      });
  }
}
