import { DecimalPipe } from '@angular/common';
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
import { MatSelectModule } from '@angular/material/select';
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
  ProductType,
  typeLabel,
  unitLabel,
} from '../product.models';

@Component({
  selector: 'app-product-list',
  imports: [
    MatButtonModule,
    DecimalPipe,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './product-list.html',
  styleUrl: './product-list.scss',
})
export class ProductList {
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  protected readonly types = PRODUCT_TYPES;
  protected readonly columns = ['code', 'name', 'type', 'onHand', 'minStock', 'status'];
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly typeLabel = typeLabel;
  protected readonly unitLabel = unitLabel;
  protected readonly errorMessage = errorMessage;

  protected readonly search = signal('');
  protected readonly type = signal<ProductType | null>(null);
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
      },
    };
  });

  /** The last loaded page, kept while the next one loads so the table does not flash empty. */
  protected readonly page = linkedSignal<Paged<Product> | undefined, Paged<Product> | undefined>({
    source: () => (this.products.hasValue() ? this.products.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

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
      .open<ProductFormDialog, ProductFormDialogData, Product>(ProductFormDialog, { data: {} })
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
