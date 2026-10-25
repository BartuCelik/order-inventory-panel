import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api';
import { CreateWorkOrderRequest, WorkOrder } from './work-order.models';

export type WorkOrderAction = 'start' | 'complete' | 'cancel';

/** Writes for work orders; reads use httpResource in the components. */
@Injectable({ providedIn: 'root' })
export class WorkOrdersApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/work-orders`;

  create(request: CreateWorkOrderRequest): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(this.baseUrl, request);
  }

  act(id: number, action: WorkOrderAction): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`${this.baseUrl}/${id}/${action}`, null);
  }
}
