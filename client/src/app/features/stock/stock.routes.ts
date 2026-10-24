import { Routes } from '@angular/router';

export const STOCK_ROUTES: Routes = [
  {
    path: '',
    title: 'Stock · Order & Inventory',
    loadComponent: () => import('./stock-ledger/stock-ledger').then((m) => m.StockLedger),
  },
];
