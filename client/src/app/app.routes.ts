import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { Shell } from './layout/shell/shell';
import { pageTitle } from './core/i18n/i18n';

export const routes: Routes = [
  {
    path: 'login',
    title: pageTitle('title.signIn'),
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: pageTitle('title.shopFloor'),
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
