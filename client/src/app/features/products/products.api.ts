import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api';
import { BomLine, Product, ProductRequest } from './product.models';

/** Writes for products and BOM lines; reads use httpResource in the components. */
@Injectable({ providedIn: 'root' })
export class ProductsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/products`;

  create(request: ProductRequest): Observable<Product> {
    return this.http.post<Product>(this.baseUrl, request);
  }

  update(id: number, request: ProductRequest): Observable<Product> {
    return this.http.put<Product>(`${this.baseUrl}/${id}`, request);
  }

  addBomLine(
    productId: number,
    componentProductId: number,
    qtyPerUnit: number,
  ): Observable<BomLine> {
    return this.http.post<BomLine>(`${this.baseUrl}/${productId}/bom`, {
      componentProductId,
      qtyPerUnit,
    });
  }

  deleteBomLine(productId: number, lineId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${productId}/bom/${lineId}`);
  }
}
