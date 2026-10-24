import { UnitOfMeasure } from '../products/product.models';

export type StockMovementType =
  'Receipt' | 'Issue' | 'Adjustment' | 'ProductionConsumption' | 'ProductionOutput';

/** The types a user can record by hand; production movements come from work orders. */
export type ManualMovementType = Extract<StockMovementType, 'Receipt' | 'Issue' | 'Adjustment'>;

export interface StockMovement {
  id: number;
  productId: number;
  productCode: string;
  productName: string;
  unit: UnitOfMeasure;
  type: StockMovementType;
  /** Signed: positive adds stock, negative removes it. */
  quantity: number;
  refType: 'SalesOrder' | 'WorkOrder' | null;
  refId: number | null;
  note: string | null;
  createdBy: string;
  createdAt: string;
}

export interface RecordMovementRequest {
  productId: number;
  type: ManualMovementType;
  /** Positive for Receipt and Issue (the type sets the direction); signed for Adjustment. */
  quantity: number;
  note: string | null;
}

export const MANUAL_MOVEMENT_TYPES: readonly {
  value: ManualMovementType;
  label: string;
  hint: string;
}[] = [
  { value: 'Receipt', label: 'Receipt', hint: 'Goods received. Stock goes up.' },
  { value: 'Issue', label: 'Issue', hint: 'Goods taken out of stock. Stock goes down.' },
  {
    value: 'Adjustment',
    label: 'Adjustment',
    hint: 'Correction after a count or a mistake. Positive adds, negative removes.',
  },
];

const TYPE_LABELS: Record<StockMovementType, string> = {
  Receipt: 'Receipt',
  Issue: 'Issue',
  Adjustment: 'Adjustment',
  ProductionConsumption: 'Production use',
  ProductionOutput: 'Production output',
};

export function movementTypeLabel(type: StockMovementType): string {
  return TYPE_LABELS[type];
}

/** CSS chip modifier for a movement type (see .chip in styles.scss). */
export function movementTypeChip(type: StockMovementType): string {
  switch (type) {
    case 'Receipt':
      return 'success';
    case 'Issue':
      return 'danger';
    case 'Adjustment':
      return 'warning';
    default:
      return 'production';
  }
}
