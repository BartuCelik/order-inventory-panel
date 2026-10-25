import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { debounceTime, filter, switchMap } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { confirm } from '../../../shared/confirm-dialog/confirm-dialog';
import { MovementTable } from '../../stock/movement-table/movement-table';
import {
  RecordMovementDialog,
  RecordMovementDialogData,
} from '../../stock/record-movement-dialog/record-movement-dialog';
import { STOCK_MOVEMENTS_URL } from '../../stock/stock.api';
import { StockMovement, movementTypeLabel } from '../../stock/stock.models';
import {
  BomLine,
  MaterialNeed,
  Paged,
  Product,
  ProductDetails,
  isBelowMinimum,
  stockLevel,
  typeChip,
  typeLabel,
  unitLabel,
} from '../product.models';
import {
  ProductFormDialog,
  ProductFormDialogData,
} from '../product-form-dialog/product-form-dialog';
import { ProductPicker } from '../product-picker/product-picker';
import { ProductsApi } from '../products.api';
import { AuthService } from '../../../core/auth/auth.service';
import { t, TranslatePipe } from '../../../core/i18n/i18n';

/** Same bound as the API's explosion endpoint. */
const MAX_EXPLOSION_QTY = 1_000_000;

@Component({
  selector: 'app-product-detail',
  imports: [
    TranslatePipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    MovementTable,
    ProductPicker,
  ],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss',
})
export class ProductDetail {
  /** Route parameter, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  private readonly api = inject(ProductsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly auth = inject(AuthService);

  protected readonly typeLabel = typeLabel;
  protected readonly typeChip = typeChip;
  protected readonly isBelowMinimum = isBelowMinimum;
  protected readonly stockLevel = stockLevel;
  protected readonly unitLabel = unitLabel;
  protected readonly errorMessage = errorMessage;
  protected readonly bomColumns = ['code', 'type', 'qtyPerUnit', 'actions'];
  protected readonly explosionColumns = ['code', 'name', 'quantity'];
  protected readonly maxQty = MAX_EXPLOSION_QTY;

  private readonly productUrl = computed(
    () => `${API_BASE_URL}/products/${encodeURIComponent(this.id())}`,
  );

  /** The product with its direct BOM lines, in one request. */
  protected readonly product = httpResource<ProductDetails>(() => this.productUrl());
  protected readonly bomLines = computed(() =>
    this.product.hasValue() ? this.product.value().bomLines : [],
  );

  /** Latest movements of this product; the full ledger is on the Stock page. */
  protected readonly movements = httpResource<Paged<StockMovement>>(() => ({
    url: STOCK_MOVEMENTS_URL,
    params: { productId: this.id(), pageSize: 10 },
  }));

  protected readonly notFound = computed(
    () => (this.product.error() as HttpErrorResponse | undefined)?.status === 404,
  );
  protected readonly isRawMaterial = computed(
    () => this.product.hasValue() && this.product.value().type === 'RawMaterial',
  );

  /** Units to explode the BOM for, as typed; null while the field is empty or not a number. */
  protected readonly quantity = signal<number | null>(1);
  protected readonly quantityValid = computed(() => {
    const qty = this.quantity();
    return qty !== null && qty > 0 && qty <= MAX_EXPLOSION_QTY;
  });
  private readonly debouncedQuantity = toSignal(
    toObservable(this.quantity).pipe(debounceTime(250)),
    { initialValue: 1 },
  );

  protected readonly explosion = httpResource<MaterialNeed[]>(() => {
    const qty = this.debouncedQuantity();
    // Raw materials have no BOM, and an invalid quantity would only produce a 400.
    if (this.isRawMaterial() || qty === null || qty <= 0 || qty > MAX_EXPLOSION_QTY) {
      return undefined;
    }
    return { url: `${this.productUrl()}/explosion`, params: { quantity: qty } };
  });

  // ---------- BOM editing ----------

  protected readonly newComponent = signal<Product | null>(null);
  protected readonly newQtyPerUnit = signal<number | null>(null);
  protected readonly bomError = signal<string | null>(null);
  protected readonly bomSaving = signal(false);

  /** The product itself and its current components cannot be picked; the API checks rules 5 and 6. */
  protected readonly excludedComponentIds = computed(() => [
    Number(this.id()),
    ...this.bomLines().map((l) => l.componentProductId),
  ]);

  protected setNewQtyPerUnit(value: string): void {
    const qty = Number(value);
    this.newQtyPerUnit.set(value.trim() === '' || !Number.isFinite(qty) ? null : qty);
  }

  protected addBomLine(): void {
    const component = this.newComponent();
    const qty = this.newQtyPerUnit();
    if (!component) {
      this.bomError.set(t('product.pickComponent'));
      return;
    }
    if (qty === null || qty <= 0) {
      this.bomError.set(t('product.qtyAboveZero'));
      return;
    }

    this.bomSaving.set(true);
    this.bomError.set(null);
    this.api.addBomLine(Number(this.id()), component.id, qty).subscribe({
      next: () => {
        this.bomSaving.set(false);
        this.newComponent.set(null);
        this.newQtyPerUnit.set(null);
        this.reloadBom();
      },
      error: (error: unknown) => {
        // 409 duplicate, 422 cycle or raw-material parent: the detail says which.
        this.bomSaving.set(false);
        this.bomError.set(errorMessage(error));
      },
    });
  }

  protected removeBomLine(line: BomLine): void {
    confirm(this.dialog, {
      title: t('product.removeTitle'),
      message: t('product.removeMessage', { code: line.componentCode }),
      confirmLabel: t('product.remove'),
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.deleteBomLine(Number(this.id()), line.id)),
      )
      .subscribe({
        next: () => this.reloadBom(),
        error: (error: unknown) => this.snackBar.open(errorMessage(error), t('common.close')),
      });
  }

  private reloadBom(): void {
    this.product.reload();
    this.explosion.reload();
  }

  // ---------- product and stock ----------

  protected edit(product: Product): void {
    this.dialog
      .open<ProductFormDialog, ProductFormDialogData, ProductDetails>(ProductFormDialog, {
        data: { product },
      })
      .afterClosed()
      .subscribe((updated) => {
        if (updated) {
          this.snackBar.open(t('product.saved', { code: updated.code }), undefined, {
            duration: 3000,
          });
          this.product.reload();
        }
      });
  }

  protected recordMovement(product: Product): void {
    this.dialog
      .open<RecordMovementDialog, RecordMovementDialogData, StockMovement>(RecordMovementDialog, {
        data: { product },
      })
      .afterClosed()
      .subscribe((movement) => {
        if (movement) {
          this.snackBar.open(
            t('stock.recorded', { type: movementTypeLabel(movement.type) }),
            undefined,
            {
              duration: 3000,
            },
          );
          this.product.reload();
          this.movements.reload();
        }
      });
  }

  protected setQuantity(value: string): void {
    const qty = Number(value);
    this.quantity.set(value.trim() === '' || !Number.isFinite(qty) ? null : qty);
  }
}
