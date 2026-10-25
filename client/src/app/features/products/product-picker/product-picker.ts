import { httpResource } from '@angular/common/http';
import { Component, computed, effect, input, model, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { debounceTime } from 'rxjs';
import { API_BASE_URL } from '../../../core/api';
import { Paged, Product, ProductType, typeLabel } from '../product.models';

/**
 * Search-as-you-type product field. Two-way bind the chosen product with [(product)];
 * it is null while the text does not match a picked option.
 */
@Component({
  selector: 'app-product-picker',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatFormFieldModule, MatInputModule],
  template: `
    <mat-form-field appearance="outline" class="field" [subscriptSizing]="subscriptSizing()">
      <mat-label>{{ label() }}</mat-label>
      <input matInput [formControl]="control" [matAutocomplete]="auto" />
      <mat-autocomplete #auto="matAutocomplete" [displayWith]="display">
        @for (p of options(); track p.id) {
          <mat-option [value]="p">
            <span class="code">{{ p.code }}</span
            >&ensp;{{ p.name }}
            <small>{{ typeLabel(p.type) }}</small>
          </mat-option>
        }
        @if (noMatch()) {
          <mat-option disabled>No matching products.</mat-option>
        }
      </mat-autocomplete>
      @if (error()) {
        <mat-hint class="error">{{ error() }}</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    .field {
      width: 100%;
    }
    .code {
      font-family: var(--hmi-condensed);
      font-weight: 600;
    }
    .error {
      color: var(--mat-sys-error);
    }
    small {
      margin-left: 8px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ProductPicker {
  readonly label = input('Product');
  /** Products that may not be picked, e.g. the parent itself and its existing components. */
  readonly excludeIds = input<readonly number[]>([]);
  readonly error = input<string | null>(null);
  /** Only offer products of this type, e.g. finished goods on a sales order (rule 13). */
  readonly type = input<ProductType | null>(null);
  readonly subscriptSizing = input<'fixed' | 'dynamic'>('dynamic');
  readonly product = model<Product | null>(null);

  protected readonly typeLabel = typeLabel;
  protected readonly control = new FormControl<string | Product>('', { nonNullable: true });

  private readonly text = signal('');
  private readonly search = toSignal(toObservable(this.text).pipe(debounceTime(250)), {
    initialValue: '',
  });

  private readonly results = httpResource<Paged<Product>>(() => {
    const search = this.search().trim();
    return search
      ? {
          url: `${API_BASE_URL}/products`,
          params: { search, pageSize: 10, ...(this.type() ? { type: this.type()! } : {}) },
        }
      : undefined;
  });

  protected readonly options = computed(() => {
    const excluded = this.excludeIds();
    return (this.results.hasValue() ? this.results.value().items : []).filter(
      (p) => !excluded.includes(p.id),
    );
  });
  protected readonly noMatch = computed(
    () => this.results.hasValue() && this.search().trim() !== '' && this.options().length === 0,
  );

  constructor() {
    this.control.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      if (typeof value === 'string') {
        this.text.set(value);
        this.product.set(null);
      } else {
        this.product.set(value);
      }
    });

    // Follow changes made by the parent, e.g. clearing the field after a save or preselecting a product.
    effect(() => {
      const product = this.product();
      const current = this.control.value;
      if (product !== current && !(product === null && typeof current === 'string')) {
        this.control.setValue(product ?? '', { emitEvent: false });
      }
    });
  }

  protected display = (value: string | Product | null): string =>
    typeof value === 'string' ? value : value ? `${value.code} · ${value.name}` : '';
}
