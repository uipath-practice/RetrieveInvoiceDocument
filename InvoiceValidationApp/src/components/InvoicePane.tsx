import type { Invoice } from '../types';
import type { HighlightMap } from '../highlight';
import { hlClass } from '../highlight';
import { amount, percent, quantity, text } from '../format';

interface Props {
  invoice: Invoice | null;
  invoiceError: string | null;
  highlights: HighlightMap;
  selected: number | null;
}

/** The invoice as IXP read it: the left half of the Compare tab, with the findings highlighted. */
export default function InvoicePane({ invoice, invoiceError, highlights, selected }: Props) {
  const cls = (key: string, rowKey?: string) => hlClass(highlights, key, selected, rowKey);

  return (
    <section className="pane" aria-label="Invoice">
      <header className="pane__head">
        <h2 className="pane__title">Invoice <span className="pane__sub">extracted data</span></h2>
        {invoice?.invoiceNumber && <span className="pane__id">{invoice.invoiceNumber}</span>}
      </header>
      <div className="pane__body">
        {invoiceError && <div className="pane__error">The invoice JSON could not be read: {invoiceError}</div>}
        {!invoice && !invoiceError && <div className="pane__empty">No invoice data was passed to the task.</div>}
        {invoice && (
          <div className="invoice">
            <div className="invoice__top">
              <div>
                <div className="invoice__label">Invoice</div>
                <div className="invoice__number">{text(invoice.invoiceNumber)}</div>
              </div>
              <table className="kv">
                <tbody>
                  <tr><th>Invoice date</th><td>{text(invoice.invoiceDate)}</td></tr>
                  <tr><th>Due date</th><td>{text(invoice.dueDate)}</td></tr>
                  <tr><th>Purchase order</th><td className={cls('po')}>{text(invoice.poNumber)}</td></tr>
                  <tr><th>Currency</th><td className={cls('currency')}>{text(invoice.currency)}</td></tr>
                </tbody>
              </table>
            </div>

            <div className="parties">
              <PartyBlock title="From (vendor)" prefix="vendor" party={invoice.vendor} cls={cls} />
              <PartyBlock title="Bill to (buyer)" prefix="buyer" party={invoice.buyer} cls={cls} />
            </div>

            <table className="lines">
              <thead>
                <tr><th>#</th><th className="t">Description</th><th className="n">Qty</th><th className="n">Unit price</th><th className="n">Line total</th></tr>
              </thead>
              <tbody>
                {(invoice.lines ?? []).map((l, i) => {
                  const n = l.n ?? i + 1;
                  const row = `line.${n}`;
                  return (
                    <tr key={`${n}-${i}`} className={cls(row)}>
                      <td>{n}</td>
                      <td className={`t ${cls(`${row}.description`, row)}`}>{text(l.description)}</td>
                      <td className={`n ${cls(`${row}.quantity`, row)}`}>{quantity(l.quantity)}</td>
                      <td className={`n ${cls(`${row}.unitPrice`, row)}`}>{amount(l.unitPrice)}</td>
                      <td className={`n ${cls(`${row}.lineTotal`, row)}`}>{amount(l.lineTotal)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            <table className="totals">
              <tbody>
                <tr><th>Subtotal</th><td className={cls('total.subtotal')}>{amount(invoice.subtotal)} {text(invoice.currency)}</td></tr>
                <tr>
                  <th><span className={cls('tax.taxName')}>{text(invoice.taxName) || 'Tax'}</span> <span className={cls('tax.taxRate')}>{percent(invoice.taxRate)}</span></th>
                  <td className={cls('tax.taxTotal')}>{amount(invoice.taxTotal)} {text(invoice.currency)}</td>
                </tr>
                <tr className="totals__grand"><th>Total</th><td className={cls('total.total')}>{amount(invoice.total)} {text(invoice.currency)}</td></tr>
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}

function PartyBlock({ title, prefix, party, cls }: { title: string; prefix: 'vendor' | 'buyer'; party: Invoice['vendor'] | undefined; cls: (k: string) => string }) {
  return (
    <div className="party">
      <div className="party__title">{title}</div>
      <div className={`party__name ${cls(`${prefix}.name`)}`}>{text(party?.name)}</div>
      <div className={cls(`${prefix}.address`)}>{text(party?.address)}</div>
      <div className={cls(`${prefix}.country`)}>{text(party?.country)}</div>
      <div className={`party__tax ${cls(`${prefix}.taxId`)}`}>Tax ID {text(party?.taxId)}</div>
    </div>
  );
}
