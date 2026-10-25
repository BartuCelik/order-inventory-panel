import { Routes } from '@angular/router';
import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Shop floor · Workshop panel',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'work-orders',
        loadChildren: () =>
          import('./features/work-orders/work-orders.routes').then((m) => m.WORK_ORDER_ROUTES),
      },
      {
        path: 'sales-orders',
        loadChildren: () =>
          import('./features/orders/orders.routes').then((m) => m.SALES_ORDER_ROUTES),
      },
      {
        path: 'customers',
        loadChildren: () =>
          import('./features/orders/orders.routes').then((m) => m.CUSTOMER_ROUTES),
      },
      {
        path: 'products',
        loadChildren: () =>
          import('./features/products/products.routes').then((m) => m.PRODUCT_ROUTES),
      },
      {
        path: 'stock',
        loadChildren: () => import('./features/stock/stock.routes').then((m) => m.STOCK_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
