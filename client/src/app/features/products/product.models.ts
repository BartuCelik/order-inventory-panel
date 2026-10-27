import { t } from '../../core/i18n/i18n';

export type ProductType = 'RawMaterial' | 'SemiFinished' | 'Finished';
export type UnitOfMeasure = 'Piece' | 'Meter' | 'SquareMeter' | 'Kilogram' | 'Liter';

export interface Product {
  id: number;
  code: string;
  name: string;
  type: ProductType;
  unit: UnitOfMeasure;
  minStock: number;
  isActive: boolean;
  /** Computed by the API from the stock ledger. */
  onHand: number;
  /** On-hand minus what open work orders still reserve (rule 9). */
  available: number;
}

/** GET /products/{id}: the product with its direct BOM lines. */
export interface ProductDetails extends Product {
  bomLines: BomLine[];
}

/** Body of POST /products. */
export interface CreateProductRequest {
  code: string;
  name: string;
  type: ProductType;
  unit: UnitOfMeasure;
  minStock: number;
}

/**
 * Body of PUT /products/{id}. Code, type and unit are fixed after creation: the ledger, BOM lines
 * and orders are expressed in the product's unit and depend on its type.
 */
export interface UpdateProductRequest {
  name: string;
  minStock: number;
  isActive: boolean;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BomLine {
  id: number;
  componentProductId: number;
  componentCode: string;
  componentName: string;
  componentType: ProductType;
  componentUnit: UnitOfMeasure;
  qtyPerUnit: number;
}

/** One leaf material of GET /products/{id}/explosion (rule 7). */
export interface MaterialNeed {
  productId: number;
  code: string;
  name: string;
  unit: UnitOfMeasure;
  quantity: number;
}

export const PRODUCT_TYPES: readonly { value: ProductType; label: string }[] = (
  ['RawMaterial', 'SemiFinished', 'Finished'] as const
).map((value) => ({ value, label: t(`productType.${value}`) }));

const TYPE_LABELS = Object.fromEntries(PRODUCT_TYPES.map((t) => [t.value, t.label])) as Record<
  ProductType,
  string
>;

const UNIT_VALUES = ['Piece', 'Meter', 'SquareMeter', 'Kilogram', 'Liter'] as const;

export const UNITS: readonly { value: UnitOfMeasure; label: string }[] = UNIT_VALUES.map(
  (value) => ({ value, label: t(`unitName.${value}`) }),
);

const UNIT_LABELS = Object.fromEntries(UNIT_VALUES.map((u) => [u, t(`unit.${u}`)])) as Record<
  UnitOfMeasure,
  string
>;

/** CSS chip modifier for a product type (see .chip in styles.scss). */
export function typeChip(type: ProductType): string {
  return type === 'RawMaterial' ? 'raw' : type === 'SemiFinished' ? 'semi' : 'finished';
}

/** True when the product has a minimum and available stock is below it, as on the dashboard. */
export function isBelowMinimum(p: { available: number; minStock: number }): boolean {
  return p.minStock > 0 && p.available < p.minStock;
}

/**
 * Width (0–100) of a stock meter: full at twice the minimum, so "just above minimum" still looks
 * half empty. Null when the product has no minimum.
 */
export function stockLevel(p: { available: number; minStock: number }): number | null {
  if (p.minStock <= 0) {
    return null;
  }
  return Math.max(0, Math.min(100, (p.available / (p.minStock * 2)) * 100));
}

export function typeLabel(type: ProductType): string {
  return TYPE_LABELS[type];
}

export function unitLabel(unit: UnitOfMeasure): string {
  return UNIT_LABELS[unit];
}
