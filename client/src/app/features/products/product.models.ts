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

export const PRODUCT_TYPES: readonly { value: ProductType; label: string }[] = [
  { value: 'RawMaterial', label: 'Raw material' },
  { value: 'SemiFinished', label: 'Semi-finished' },
  { value: 'Finished', label: 'Finished' },
];

const TYPE_LABELS = Object.fromEntries(PRODUCT_TYPES.map((t) => [t.value, t.label])) as Record<
  ProductType,
  string
>;

export const UNITS: readonly { value: UnitOfMeasure; label: string }[] = [
  { value: 'Piece', label: 'Piece (pcs)' },
  { value: 'Meter', label: 'Meter (m)' },
  { value: 'SquareMeter', label: 'Square meter (m²)' },
  { value: 'Kilogram', label: 'Kilogram (kg)' },
  { value: 'Liter', label: 'Liter (L)' },
];

const UNIT_LABELS: Record<UnitOfMeasure, string> = {
  Piece: 'pcs',
  Meter: 'm',
  SquareMeter: 'm²',
  Kilogram: 'kg',
  Liter: 'L',
};

export function typeLabel(type: ProductType): string {
  return TYPE_LABELS[type];
}

export function unitLabel(unit: UnitOfMeasure): string {
  return UNIT_LABELS[unit];
}
