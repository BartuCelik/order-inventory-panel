import { Routes } from '@angular/router';

export const PRODUCT_ROUTES: Routes = [
  {
    path: '',
    title: 'Products · Order & Inventory',
    loadComponent: () => import('./product-list/product-list').then((m) => m.ProductList),
  },
  {
    path: ':id',
    title: 'Product · Order & Inventory',
    loadComponent: () => import('./product-detail/product-detail').then((m) => m.ProductDetail),
  },
];
