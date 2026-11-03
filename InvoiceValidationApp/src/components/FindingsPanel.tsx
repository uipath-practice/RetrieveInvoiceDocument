import type { Finding, MatchResult } from '../types';
import { severityOf } from '../highlight';

interface Props {
  result: MatchResult | null;
  error: string | null;
  selected: number | null;
  onSelect: (index: number | null) => void;
}

const LABEL: Record<string, string> = { red: 'Red', yellow: 'Yellow', review: 'Review' };

/** One row of finding chips above the two documents; the selected one shows its values and note on a second line. */
export default function FindingsPanel({ result, error, selected, onSelect }: Props) {
  if (error) return <div className="findings-bar"><div className="pane__error">The match result JSON could not be read: {error}</div></div>;
  if (!result) return <div className="findings-bar"><span className="findings-bar__none">No match result was passed to the task.</span></div>;

  const current = selected != null ? result.findings[selected] : undefined;

  return (
    <div className="findings-bar">
      <div className="findings-bar__row">
        <span className="findings-bar__label">{result.findings.length === 0 ? 'No findings' : `${result.findings.length} finding${result.findings.length === 1 ? '' : 's'}`}</span>
        {result.findings.length === 0 && <span className="findings-bar__none">The invoice and the purchase order agree on every compared field.</span>}
        <div className="findings-bar__chips">
          {result.findings.map((f, i) => (
            <button
              key={i}
              type="button"
              className={`chip chip--${severityOf(f)}${selected === i ? ' chip--on' : ''}`}
              onClick={() => onSelect(selected === i ? null : i)}
              aria-pressed={selected === i}
              title={f.note}
            >
              <span className={`sev sev--${severityOf(f)}`}>{LABEL[severityOf(f)]}</span>
              {where(f)}
            </button>
          ))}
        </div>
      </div>
      {current && (
        <div className={`findings-bar__detail findings-bar__detail--${severityOf(current)}`}>
          <span className="finding__val"><span className="finding__k">Invoice</span> {current.invoiceValue || '—'}</span>
          <span className="finding__val"><span className="finding__k">Order</span> {current.poValue || '—'}</span>
          {current.note && <span className="finding__note">{current.note}</span>}
        </div>
      )}
    </div>
  );
}

function where(f: Finding): string {
  const parts: string[] = [];
  if (f.area === 'line') {
    if (f.invoiceLine != null && f.poLine != null) parts.push(f.invoiceLine === f.poLine ? `Line ${f.invoiceLine}` : `Inv. line ${f.invoiceLine} / order line ${f.poLine}`);
    else if (f.invoiceLine != null) parts.push(`Invoice line ${f.invoiceLine} (not ordered)`);
    else if (f.poLine != null) parts.push(`Order line ${f.poLine} (not invoiced)`);
    else parts.push('Line');
  } else if (f.area) parts.push(f.area.charAt(0).toUpperCase() + f.area.slice(1));
  if (f.field) parts.push(f.field);
  return parts.join(' · ');
}
