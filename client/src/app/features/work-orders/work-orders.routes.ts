import { Routes } from '@angular/router';

export const WORK_ORDER_ROUTES: Routes = [
  {
    path: '',
    title: 'Work orders · Workshop panel',
    loadComponent: () => import('./work-order-list/work-order-list').then((m) => m.WorkOrderList),
  },
  {
    path: ':id',
    title: 'Work order · Workshop panel',
    loadComponent: () =>
      import('./work-order-detail/work-order-detail').then((m) => m.WorkOrderDetail),
  },
];
