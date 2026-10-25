import { Routes } from '@angular/router';
import { pageTitle } from '../../core/i18n/i18n';

export const WORK_ORDER_ROUTES: Routes = [
  {
    path: '',
    title: pageTitle('title.workOrders'),
    loadComponent: () => import('./work-order-list/work-order-list').then((m) => m.WorkOrderList),
  },
  {
    path: ':id',
    title: pageTitle('title.workOrder'),
    loadComponent: () =>
      import('./work-order-detail/work-order-detail').then((m) => m.WorkOrderDetail),
  },
];
