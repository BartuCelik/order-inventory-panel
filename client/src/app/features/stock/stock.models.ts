import { UnitOfMeasure } from '../products/product.models';
import { t } from '../../core/i18n/i18n';

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
}[] = (['Receipt', 'Issue', 'Adjustment'] as const).map((value) => ({
  value,
  label: t(`movementType.${value}`),
  hint: t(`movementHint.${value}`),
}));

export function movementTypeLabel(type: StockMovementType): string {
  return t(`movementType.${type}`);
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
