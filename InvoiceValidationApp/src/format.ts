/** Amounts as the documents print them: 1,234.50 (the generator's Formatting.cs does the same). */
export function amount(n: number | null | undefined): string {
  if (n == null || !Number.isFinite(Number(n))) return '';
  return new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(n));
}

export function quantity(n: number | null | undefined): string {
  if (n == null || !Number.isFinite(Number(n))) return '';
  const v = Number(n);
  return Number.isInteger(v) ? String(v) : amount(v);
}

export function percent(n: number | null | undefined): string {
  if (n == null || !Number.isFinite(Number(n))) return '';
  const v = Number(n);
  return `${Number.isInteger(v) ? v : v.toFixed(2)}%`;
}

/** SAP prints dates as DD.MM.YYYY. */
export function sapDate(iso: string | null | undefined): string {
  if (!iso) return '';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  return m ? `${m[3]}.${m[2]}.${m[1]}` : iso;
}

export function text(v: unknown): string {
  return v == null ? '' : String(v);
}
