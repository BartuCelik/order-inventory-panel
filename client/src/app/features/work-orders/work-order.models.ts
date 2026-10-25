import { ProductType, UnitOfMeasure } from '../products/product.models';
import { t } from '../../core/i18n/i18n';

export type WorkOrderStatus = 'Planned' | 'InProgress' | 'Done' | 'Cancelled';

export const WORK_ORDER_STATUSES: { value: WorkOrderStatus; label: string }[] = (
  ['Planned', 'InProgress', 'Done', 'Cancelled'] as const
).map((value) => ({ value, label: t(`workStatus.${value}`) }));

/** A row of GET /work-orders. hasShortage is the rule 9 planning signal; always false when closed. */
export interface WorkOrderSummary {
  id: number;
  number: string;
  productId: number;
  productCode: string;
  productName: string;
  unit: UnitOfMeasure;
  quantity: number;
  status: WorkOrderStatus;
  plannedDate: string;
  completedAt: string | null;
  salesOrderId: number | null;
  salesOrderNumber: string | null;
  customerName: string | null;
  hasShortage: boolean;
}

/** GET /work-orders/{id} */
export interface WorkOrder extends WorkOrderSummary {
  salesOrderLineId: number | null;
  components: WorkOrderComponent[];
}

/** A snapshotted BOM line (rule 8) with its availability (rule 9). */
export interface WorkOrderComponent {
  productId: number;
  productCode: string;
  productName: string;
  type: ProductType;
  unit: UnitOfMeasure;
  requiredQty: number;
  consumedQty: number;
  onHand: number;
  /** Still needed by the other open work orders. */
  reserved: number;
  available: number;
  shortage: number;
}

export interface CreateWorkOrderRequest {
  productId: number;
  quantity: number;
  plannedDate?: string;
  salesOrderLineId?: number;
}

export function statusLabel(status: WorkOrderStatus): string {
  return WORK_ORDER_STATUSES.find((s) => s.value === status)?.label ?? status;
}

/** CSS modifier for .status (see styles.scss): the indicator's shape encodes the state. */
export function statusClass(status: WorkOrderStatus | string): string {
  switch (status) {
    case 'InProgress':
      return 'in-progress';
    case 'Done':
    case 'Shipped':
      return 'done';
    case 'Cancelled':
      return 'cancelled';
    default:
      return 'planned';
  }
}

export function isOpen(status: WorkOrderStatus): boolean {
  return status === 'Planned' || status === 'InProgress';
}
