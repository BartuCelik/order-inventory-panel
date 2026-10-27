import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, tap } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { errorMessage } from '../../../core/problem';
import {
  ProductFormDialog,
  ProductFormDialogData,
} from '../product-form-dialog/product-form-dialog';
import {
  PRODUCT_TYPES,
  Paged,
  Product,
  ProductDetails,
  ProductType,
  isBelowMinimum,
  stockLevel,
  typeChip,
  typeLabel,
  unitLabel,
} from '../product.models';
import { AuthService } from '../../../core/auth/auth.service';
import { TranslatePipe } from '../../../core/i18n/i18n';

@Component({
  selector: 'app-product-list',
  imports: [
    TranslatePipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTableModule,
  ],
  templateUrl: './product-list.html',
  styleUrl: './product-list.scss',
})
export class ProductList {
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  protected readonly auth = inject(AuthService);

  protected readonly types = PRODUCT_TYPES;
  protected readonly columns = ['product', 'type', 'onHand', 'available', 'minStock', 'status'];
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly typeLabel = typeLabel;
  protected readonly typeChip = typeChip;
  protected readonly unitLabel = unitLabel;
  protected readonly isBelowMinimum = isBelowMinimum;
  protected readonly stockLevel = stockLevel;
  protected readonly errorMessage = errorMessage;

  protected readonly search = signal('');
  protected readonly type = signal<ProductType | null>(null);
  protected readonly includeInactive = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);

  /** Waits for a pause in typing, then starts again from the first page. */
  private readonly debouncedSearch = toSignal(
    toObservable(this.search).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      tap(() => this.pageIndex.set(0)),
    ),
    { initialValue: '' },
  );

  protected readonly products = httpResource<Paged<Product>>(() => {
    const search = this.debouncedSearch().trim();
    const type = this.type();
    return {
      url: `${API_BASE_URL}/products`,
      params: {
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        ...(search ? { search } : {}),
        ...(type ? { type } : {}),
        ...(this.includeInactive() ? { includeInactive: true } : {}),
      },
    };
  });

  /** The last loaded page, kept while the next one loads so the table does not flash empty. */
  protected readonly page = linkedSignal<Paged<Product> | undefined, Paged<Product> | undefined>({
    source: () => (this.products.hasValue() ? this.products.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

  protected setIncludeInactive(value: boolean): void {
    this.includeInactive.set(value);
    this.pageIndex.set(0);
  }

  protected setType(type: ProductType | null): void {
    this.type.set(type);
    this.pageIndex.set(0);
  }

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected create(): void {
    this.dialog
      .open<ProductFormDialog, ProductFormDialogData, ProductDetails>(ProductFormDialog, {
        data: {},
      })
      .afterClosed()
      .subscribe((product) => {
        if (product) {
          this.open(product);
        }
      });
  }

  protected open(product: Product): void {
    this.router.navigate(['/products', product.id]);
  }
}
