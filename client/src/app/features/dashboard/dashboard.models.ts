import { UnitOfMeasure } from '../products/product.models';
import { StockMovement } from '../stock/stock.models';

/** GET /dashboard */
export interface DashboardSummary {
  products: ProductCounts;
  belowMinimum: BelowMinimum[];
  recentMovements: StockMovement[];
  /** Fourteen UTC days, oldest first, ending today; quiet days have zero counts. */
  activity: ActivityDay[];
}

/** Active products per type, and the inactive ones. */
export interface ProductCounts {
  rawMaterial: number;
  semiFinished: number;
  finished: number;
  inactive: number;
}

export interface BelowMinimum {
  productId: number;
  code: string;
  name: string;
  unit: UnitOfMeasure;
  onHand: number;
  minStock: number;
}

/** Movements that added (in) or removed (out) stock on one day. */
export interface ActivityDay {
  date: string;
  in: number;
  out: number;
}
