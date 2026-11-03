import type { Finding, Side } from './types';

/**
 * Where a finding lands on a rendered document. Keys:
 *   vendor.<field> | buyer.<field> | tax.taxName | tax.taxRate | tax.taxTotal | currency | po
 *   total.subtotal | total.total | line.<n> (whole row) | line.<n>.<field>
 */
export interface Highlight {
  severity: 'red' | 'yellow' | 'review';
  findings: number[]; // indexes into matchResult.findings
}

export type HighlightMap = Map<string, Highlight>;

const PARTY_FIELDS = ['name', 'address', 'country', 'taxId'];
const LINE_FIELDS = ['description', 'quantity', 'unitPrice', 'lineTotal'];

export function severityOf(f: Finding): Highlight['severity'] {
  const s = (f.severity || '').toLowerCase();
  return s === 'red' ? 'red' : s === 'yellow' ? 'yellow' : 'review';
}

/** The keys one finding highlights on one side of the screen. */
export function keysFor(f: Finding, side: Side): string[] {
  const area = f.area.toLowerCase();
  const field = normaliseField(area, f.field);
  switch (area) {
    case 'line': {
      const n = side === 'invoice' ? f.invoiceLine : f.poLine;
      if (n == null) return [];
      return field && LINE_FIELDS.includes(field) ? [`line.${n}.${field}`] : [`line.${n}`];
    }
    case 'vendor':
    case 'buyer':
      return field && PARTY_FIELDS.includes(field) ? [`${area}.${field}`] : PARTY_FIELDS.map((p) => `${area}.${p}`);
    case 'tax':
      return field ? [`tax.${field}`] : ['tax.taxRate', 'tax.taxTotal'];
    case 'currency':
      return ['currency'];
    case 'po':
      return ['po'];
    case 'total':
      return field ? [`total.${field}`] : ['total.total'];
    default:
      return [];
  }
}

/** Accept the agent's spelling variants: "tax rate", "TaxRate", "unit price", "amount" … */
function normaliseField(area: string, raw: string): string {
  const f = (raw || '').replace(/[\s_-]+/g, '').toLowerCase();
  if (!f) return '';
  if (area === 'tax') {
    if (f.includes('rate') || f.includes('percent')) return 'taxRate';
    if (f.includes('name') || f.includes('type')) return 'taxName';
    return 'taxTotal';
  }
  if (area === 'total') {
    if (f.includes('sub') || f.includes('net')) return 'subtotal';
    if (f.includes('tax')) return 'taxTotal';
    return 'total';
  }
  if (area === 'line') {
    if (f.startsWith('desc') || f.includes('text') || f.includes('item') || f.includes('article')) return 'description';
    if (f.startsWith('q') || f.includes('qty')) return 'quantity';
    if (f.includes('unit') || f === 'price') return 'unitPrice';
    if (f.includes('total') || f.includes('amount') || f.includes('value')) return 'lineTotal';
    return '';
  }
  if (area === 'vendor' || area === 'buyer') {
    if (f.includes('tax') || f.includes('vat') || f.includes('id')) return 'taxId';
    if (f.includes('addr') || f.includes('street')) return 'address';
    if (f.includes('country')) return 'country';
    if (f.includes('name')) return 'name';
    return '';
  }
  return raw;
}

const RANK = { review: 0, yellow: 1, red: 2 } as const;

export function buildHighlights(findings: Finding[], side: Side): HighlightMap {
  const map: HighlightMap = new Map();
  findings.forEach((f, i) => {
    const sev = severityOf(f);
    for (const key of keysFor(f, side)) {
      const cur = map.get(key);
      if (!cur) map.set(key, { severity: sev, findings: [i] });
      else {
        cur.findings.push(i);
        if (RANK[sev] > RANK[cur.severity]) cur.severity = sev;
      }
    }
  });
  return map;
}

/** CSS classes for a cell: its own key and, for a line cell, the whole-row key too. */
export function hlClass(map: HighlightMap, key: string, selected: number | null, rowKey?: string): string {
  const h = map.get(key) ?? (rowKey ? map.get(rowKey) : undefined);
  if (!h) return '';
  const active = selected != null && h.findings.includes(selected);
  return `hl hl-${h.severity}${active ? ' hl-active' : ''}`;
}
