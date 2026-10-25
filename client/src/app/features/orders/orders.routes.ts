import { Routes } from '@angular/router';

export const SALES_ORDER_ROUTES: Routes = [
  {
    path: '',
    title: 'Sales orders · Workshop panel',
    loadComponent: () =>
      import('./sales-order-list/sales-order-list').then((m) => m.SalesOrderList),
  },
  {
    path: 'new',
    title: 'New sales order · Workshop panel',
    loadComponent: () => import('./sales-order-new/sales-order-new').then((m) => m.SalesOrderNew),
  },
  {
    path: ':id',
    title: 'Sales order · Workshop panel',
    loadComponent: () =>
      import('./sales-order-detail/sales-order-detail').then((m) => m.SalesOrderDetail),
  },
];

export const CUSTOMER_ROUTES: Routes = [
  {
    path: '',
    title: 'Customers · Workshop panel',
    loadComponent: () => import('./customer-list/customer-list').then((m) => m.CustomerList),
  },
];
