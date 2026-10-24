import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import { BomExplosion, BomLine, Product, typeLabel, unitLabel } from '../product.models';

/** Same bound as the API's explosion endpoint. */
const MAX_EXPLOSION_QTY = 1_000_000;

@Component({
  selector: 'app-product-detail',
  imports: [
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
  ],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss',
})
export class ProductDetail {
  /** Route parameter, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly typeLabel = typeLabel;
  protected readonly unitLabel = unitLabel;
  protected readonly errorMessage = errorMessage;
  protected readonly bomColumns = ['code', 'name', 'type', 'qtyPerUnit'];
  protected readonly explosionColumns = ['code', 'name', 'type', 'quantity'];
  protected readonly maxQty = MAX_EXPLOSION_QTY;

  private readonly productUrl = computed(
    () => `${API_BASE_URL}/products/${encodeURIComponent(this.id())}`,
  );

  protected readonly product = httpResource<Product>(() => this.productUrl());
  protected readonly bom = httpResource<BomLine[]>(() => `${this.productUrl()}/bom`);

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

  protected readonly explosion = httpResource<BomExplosion>(() => {
    const qty = this.debouncedQuantity();
    // Raw materials have no BOM, and an invalid quantity would only produce a 400.
    if (this.isRawMaterial() || qty === null || qty <= 0 || qty > MAX_EXPLOSION_QTY) {
      return undefined;
    }
    return { url: `${this.productUrl()}/bom/explosion`, params: { qty } };
  });

  protected setQuantity(value: string): void {
    const qty = Number(value);
    this.quantity.set(value.trim() === '' || !Number.isFinite(qty) ? null : qty);
  }
}
