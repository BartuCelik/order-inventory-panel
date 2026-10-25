import { UnitOfMeasure } from '../products/product.models';
import { WorkOrderStatus } from '../work-orders/work-order.models';

export interface Customer {
  id: number;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
}

export interface CustomerRequest {
  name: string;
  contactName?: string | null;
  email?: string | null;
  phone?: string | null;
}

export type SalesOrderStatus = 'Open' | 'Shipped' | 'Cancelled';

export const SALES_ORDER_STATUSES: { value: SalesOrderStatus; label: string }[] = [
  { value: 'Open', label: 'Open' },
  { value: 'Shipped', label: 'Shipped' },
  { value: 'Cancelled', label: 'Cancelled' },
];

/** A row of GET /sales-orders. isLate is derived by the API (rule 15). */
export interface SalesOrderSummary {
  id: number;
  number: string;
  customerId: number;
  customerName: string;
  orderDate: string;
  dueDate: string;
  status: SalesOrderStatus;
  shippedAt: string | null;
  isLate: boolean;
}

export interface SalesOrder extends SalesOrderSummary {
  lines: SalesOrderLine[];
}

export interface SalesOrderLine {
  id: number;
  productId: number;
  productCode: string;
  productName: string;
  unit: UnitOfMeasure;
  quantity: number;
  /** The finished good's current on-hand: enough to ship (rule 14)? */
  onHand: number;
  workOrders: LineWorkOrder[];
}

export interface LineWorkOrder {
  id: number;
  number: string;
  quantity: number;
  status: WorkOrderStatus;
}

export interface CreateSalesOrderRequest {
  customerId: number;
  orderDate?: string;
  dueDate: string;
  lines: { productId: number; quantity: number }[];
}

/** CSS modifier for .status: open = hollow, shipped = tick, cancelled = struck. */
export function orderStatusClass(status: SalesOrderStatus): string {
  return status === 'Shipped' ? 'done' : status === 'Cancelled' ? 'cancelled' : 'planned';
}

/** Whole days between today and a due date; negative when overdue. */
export function daysUntil(date: string, today = new Date()): number {
  const due = new Date(date + 'T00:00:00');
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  return Math.round((due.getTime() - start.getTime()) / 86_400_000);
}

/** "Due today", "Due in 3 days", "2 days late". */
export function dueLabel(date: string, today = new Date()): string {
  const days = daysUntil(date, today);
  if (days === 0) return 'Due today';
  if (days === 1) return 'Due tomorrow';
  if (days > 1) return `Due in ${days} days`;
  return days === -1 ? '1 day late' : `${-days} days late`;
}

/** yyyy-MM-dd in local time, the format the API takes for DateOnly. */
export function isoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
