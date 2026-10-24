import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api';
import { RecordMovementRequest, StockMovement } from './stock.models';

export const STOCK_MOVEMENTS_URL = `${API_BASE_URL}/stock/movements`;

@Injectable({ providedIn: 'root' })
export class StockApi {
  private readonly http = inject(HttpClient);

  record(request: RecordMovementRequest): Observable<StockMovement> {
    return this.http.post<StockMovement>(STOCK_MOVEMENTS_URL, request);
  }
}
