// The exercise contract (courses/coding-agents/cookbook/environment/invoice-matching-contract.json), typed.
// The generator (RetrieveInvoiceDocument/Models/Documents.cs, Manifest.cs) is the source of truth for the shapes.

export interface Party {
  name: string;
  address: string;
  country: string;
  taxId: string;
  contact?: string;
  phone?: string;
}

export interface Line {
  n: number;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  deliveryDate?: string;
  material?: string;
}

export interface Invoice {
  invoiceNumber: string;
  poNumber: string;
  invoiceDate: string;
  dueDate: string;
  currency: string;
  vendor: Party;
  buyer: Party;
  lines: Line[];
  subtotal: number;
  taxName: string;
  taxRate: number;
  taxTotal: number;
  total: number;
}

export interface PurchaseOrder {
  poNumber: string;
  poDate: string;
  currency: string;
  vendor: Party;
  buyer: Party;
  lines: Line[];
  subtotal: number;
  taxName: string;
  taxRate: number;
  taxTotal: number;
  total: number;
}

/** yellow | red from the agent; empty when the deterministic compare wrote the finding. */
export type Severity = 'yellow' | 'red' | '';

export interface Finding {
  area: string;
  field: string;
  invoiceLine: number | null;
  poLine: number | null;
  severity: Severity | string;
  invoiceValue: string;
  poValue: string;
  note: string;
}

export interface MatchResult {
  status: string;
  findings: Finding[];
  summary: string;
  rejectionEmailHtml: string;
}

/** One property per field of action-schema.json (inputs + inOuts + outputs). */
export interface FormData {
  InvoiceFileName: string;
  InvoiceJSON: string;
  POID: string;
  MatchResultJSON: string;
  RejectionEmailHtml: string;
  ReviewerComment: string;
  ReviewerEmail: string;
}

export const defaultFormData: FormData = {
  InvoiceFileName: '',
  InvoiceJSON: '',
  POID: '',
  MatchResultJSON: '',
  RejectionEmailHtml: '',
  ReviewerComment: '',
  ReviewerEmail: '',
};

export type Side = 'invoice' | 'po';

/** Parse JSON text leniently: an object passes through, a string is parsed, anything else is null. */
export function parseJson<T>(value: unknown): { value: T | null; error: string | null } {
  if (value == null || value === '') return { value: null, error: null };
  if (typeof value === 'object') return { value: value as T, error: null };
  try {
    return { value: JSON.parse(String(value)) as T, error: null };
  } catch (e) {
    return { value: null, error: e instanceof Error ? e.message : String(e) };
  }
}

export function normaliseMatchResult(raw: Partial<MatchResult> | null): MatchResult {
  return {
    status: (raw?.status ?? '').toString().toLowerCase(),
    findings: Array.isArray(raw?.findings)
      ? raw!.findings.map((f) => ({
          area: (f?.area ?? '').toString().toLowerCase(),
          field: (f?.field ?? '').toString(),
          invoiceLine: toLine(f?.invoiceLine),
          poLine: toLine(f?.poLine),
          severity: (f?.severity ?? '').toString().toLowerCase(),
          invoiceValue: f?.invoiceValue == null ? '' : String(f.invoiceValue),
          poValue: f?.poValue == null ? '' : String(f.poValue),
          note: f?.note == null ? '' : String(f.note),
        }))
      : [],
    summary: raw?.summary == null ? '' : String(raw.summary),
    rejectionEmailHtml: raw?.rejectionEmailHtml == null ? '' : String(raw.rejectionEmailHtml),
  };
}

function toLine(v: unknown): number | null {
  if (v == null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
