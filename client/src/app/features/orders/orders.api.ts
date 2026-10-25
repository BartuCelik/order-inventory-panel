import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api';
import { CreateSalesOrderRequest, Customer, CustomerRequest, SalesOrder } from './order.models';

/** Writes for customers and sales orders; reads use httpResource in the components. */
@Injectable({ providedIn: 'root' })
export class OrdersApi {
  private readonly http = inject(HttpClient);

  createCustomer(request: CustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(`${API_BASE_URL}/customers`, request);
  }

  updateCustomer(id: number, request: CustomerRequest): Observable<Customer> {
    return this.http.put<Customer>(`${API_BASE_URL}/customers/${id}`, request);
  }

  createSalesOrder(request: CreateSalesOrderRequest): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`${API_BASE_URL}/sales-orders`, request);
  }

  ship(id: number): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`${API_BASE_URL}/sales-orders/${id}/ship`, null);
  }

  cancel(id: number): Observable<SalesOrder> {
    return this.http.post<SalesOrder>(`${API_BASE_URL}/sales-orders/${id}/cancel`, null);
  }
}
