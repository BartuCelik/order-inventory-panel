import { Routes } from '@angular/router';
import { pageTitle } from '../../core/i18n/i18n';

export const STOCK_ROUTES: Routes = [
  {
    path: '',
    title: pageTitle('title.stock'),
    loadComponent: () => import('./stock-ledger/stock-ledger').then((m) => m.StockLedger),
  },
];
