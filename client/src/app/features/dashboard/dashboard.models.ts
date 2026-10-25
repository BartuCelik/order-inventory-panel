import { UnitOfMeasure } from '../products/product.models';
import { StockMovement } from '../stock/stock.models';
import { WorkOrderSummary } from '../work-orders/work-order.models';

/** GET /dashboard */
export interface DashboardSummary {
  products: ProductCounts;
  belowMinimum: BelowMinimum[];
  recentMovements: StockMovement[];
  /** Fourteen UTC days, oldest first, ending today; quiet days have zero counts. */
  activity: ActivityDay[];
  orders: OrderStats;
  production: ProductionStats;
}

/** Open sales orders, how many are late, and the next ones due (late first). */
export interface OrderStats {
  open: number;
  late: number;
  dueSoon: DueOrder[];
}

export interface DueOrder {
  id: number;
  number: string;
  customerName: string;
  dueDate: string;
  isLate: boolean;
}

/** Work orders per open status, recent completions, short ones (rule 9) and the queue in plan order. */
export interface ProductionStats {
  planned: number;
  inProgress: number;
  doneLast7Days: number;
  shortCount: number;
  queue: WorkOrderSummary[];
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
  /** On-hand minus what open work orders still reserve; this is what is compared with the minimum. */
  available: number;
  minStock: number;
}

/** Movements that added (in) or removed (out) stock on one day. */
export interface ActivityDay {
  date: string;
  in: number;
  out: number;
}
