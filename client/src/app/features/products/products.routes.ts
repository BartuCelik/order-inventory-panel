import { Routes } from '@angular/router';
import { pageTitle } from '../../core/i18n/i18n';

export const PRODUCT_ROUTES: Routes = [
  {
    path: '',
    title: pageTitle('title.products'),
    loadComponent: () => import('./product-list/product-list').then((m) => m.ProductList),
  },
  {
    path: ':id',
    title: pageTitle('title.product'),
    loadComponent: () => import('./product-detail/product-detail').then((m) => m.ProductDetail),
  },
];
