import { Routes } from '@angular/router';
import { pageTitle } from '../../core/i18n/i18n';

export const SALES_ORDER_ROUTES: Routes = [
  {
    path: '',
    title: pageTitle('title.salesOrders'),
    loadComponent: () =>
      import('./sales-order-list/sales-order-list').then((m) => m.SalesOrderList),
  },
  {
    path: 'new',
    title: pageTitle('title.newSalesOrder'),
    loadComponent: () => import('./sales-order-new/sales-order-new').then((m) => m.SalesOrderNew),
  },
  {
    path: ':id',
    title: pageTitle('title.salesOrder'),
    loadComponent: () =>
      import('./sales-order-detail/sales-order-detail').then((m) => m.SalesOrderDetail),
  },
];

export const CUSTOMER_ROUTES: Routes = [
  {
    path: '',
    title: pageTitle('title.customers'),
    loadComponent: () => import('./customer-list/customer-list').then((m) => m.CustomerList),
  },
];
