import type { PurchaseOrder } from '../types';
import type { HighlightMap } from '../highlight';
import { hlClass } from '../highlight';
import { amount, percent, quantity, sapDate, text } from '../format';

interface Props {
  poid: string;
  po: PurchaseOrder | null;
  loading: boolean;
  error: string | null;
  highlights: HighlightMap;
  selected: number | null;
}

/** The purchase order as the SAP ME23N screen the generator renders (Templates/purchase-order-sap.html), with highlights. */
export default function PurchaseOrderPane({ poid, po, loading, error, highlights, selected }: Props) {
  const cls = (key: string, rowKey?: string) => hlClass(highlights, key, selected, rowKey);
  const vendorNumber = po ? '10' + ((po.vendor?.taxId ?? '').replace(/\D/g, '').slice(0, 5)).padStart(5, '0') : '';

  return (
    <section className="pane" aria-label="Purchase order">
      <header className="pane__head">
        <h2 className="pane__title">Purchase order <span className="pane__sub">from PurchaseOrders</span></h2>
        {poid && <span className="pane__id">{poid}</span>}
      </header>
      <div className="pane__body">
        {error && <div className="pane__error">{error}</div>}
        {loading && !po && <div className="pane__empty">Loading purchase order {poid} from Data Fabric…</div>}
        {!loading && !po && !error && <div className="pane__empty">{poid ? `Purchase order ${poid} was not found in PurchaseOrders.` : 'No purchase order number was passed to the task.'}</div>}
        {po && (
          <div className="sap">
            <div className="sap__titlebar">Display Standard PO <span className={cls('po')}>{text(po.poNumber)}</span> : Created by PURCH01</div>
            <div className="sap__toolbar"><span>Document Overview On</span><span>Print Preview</span><span>Messages</span><span>Personal Setting</span></div>
            <div className="sap__doc">
              <div className="sap__head">Standard PO &nbsp; {text(po.poNumber)} &nbsp;&nbsp; Vendor &nbsp; {vendorNumber} &nbsp; <span className={cls('vendor.name')}>{text(po.vendor?.name)}</span> &nbsp;&nbsp; Doc. date &nbsp; {sapDate(po.poDate)}</div>
              <div className="sap__tabs"><span>Delivery/Invoice</span><span>Conditions</span><span>Texts</span><span className="on">Address</span><span>Communication</span><span>Partners</span><span>Additional Data</span><span className="on">Org. Data</span><span>Status</span></div>
              <div className="sap__form">
                <table className="sap__fields">
                  <tbody>
                    <tr><td className="l">Vendor</td><td className="v">{vendorNumber}</td></tr>
                    <tr><td className="l">Name</td><td className={`v ${cls('vendor.name')}`}>{text(po.vendor?.name)}</td></tr>
                    <tr><td className="l">Street / House no.</td><td className={`v ${cls('vendor.address')}`}>{text(po.vendor?.address)}</td></tr>
                    <tr><td className="l">Country</td><td className={`v ${cls('vendor.country')}`}>{text(po.vendor?.country)}</td></tr>
                    <tr><td className="l">Tax Number 1</td><td className={`v ${cls('vendor.taxId')}`}>{text(po.vendor?.taxId)}</td></tr>
                    <tr><td className="l">Contact person</td><td className="v">{text(po.vendor?.contact)}</td></tr>
                    <tr><td className="l">Telephone</td><td className="v">{text(po.vendor?.phone)}</td></tr>
                  </tbody>
                </table>
                <table className="sap__fields">
                  <tbody>
                    <tr><td className="l">Purch. Org.</td><td className="v">1000 &nbsp; Central Purchasing</td></tr>
                    <tr><td className="l">Purch. Group</td><td className="v">001 &nbsp; Office &amp; IT</td></tr>
                    <tr><td className="l">Company Code</td><td className={`v ${cls('buyer.name')}`}>1000 &nbsp; {text(po.buyer?.name)}</td></tr>
                    <tr><td className="l">Company Address</td><td className={`v ${cls('buyer.address')} ${cls('buyer.country')}`}>{text(po.buyer?.address)}, {text(po.buyer?.country)}</td></tr>
                    <tr><td className="l">Company Tax No.</td><td className={`v ${cls('buyer.taxId')}`}>{text(po.buyer?.taxId)}</td></tr>
                    <tr><td className="l">Currency</td><td className={`v ${cls('currency')}`}>{text(po.currency)}</td></tr>
                    <tr><td className="l">Payment terms</td><td className="v">Z030 &nbsp; 30 days net</td></tr>
                  </tbody>
                </table>
              </div>

              <div className="sap__head">Item Overview</div>
              <div className="sap__gridwrap">
                <table className="sap__grid">
                  <thead><tr><th>Itm</th><th>Material</th><th>Short Text</th><th>PO Quantity</th><th>OUn</th><th>C</th><th>Deliv. Date</th><th>Net Price</th><th>Curr.</th><th>Per</th><th>OPU</th><th>Net Value</th></tr></thead>
                  <tbody>
                    {(po.lines ?? []).map((l, i) => {
                      const n = l.n ?? i + 1;
                      const row = `line.${n}`;
                      return (
                        <tr key={`${n}-${i}`} className={cls(row)}>
                          <td className="n">{n * 10}</td>
                          <td>{text(l.material)}</td>
                          <td className={`t ${cls(`${row}.description`, row)}`}>{text(l.description)}</td>
                          <td className={`n ${cls(`${row}.quantity`, row)}`}>{quantity(l.quantity)}</td>
                          <td>EA</td><td>D</td>
                          <td>{sapDate(l.deliveryDate)}</td>
                          <td className={`n ${cls(`${row}.unitPrice`, row)}`}>{amount(l.unitPrice)}</td>
                          <td>{text(po.currency)}</td><td className="n">1</td><td>EA</td>
                          <td className={`n ${cls(`${row}.lineTotal`, row)}`}>{amount(l.lineTotal)}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              <table className="sap__sum">
                <tbody>
                  <tr><td className="l">Items</td><td className="v">{(po.lines ?? []).length}</td><td className="l">Net value</td><td className={`v ${cls('total.subtotal')}`}>{amount(po.subtotal)} {text(po.currency)}</td></tr>
                  <tr><td className="l">Tax</td><td className="v"><span className={cls('tax.taxName')}>{text(po.taxName)}</span> <span className={cls('tax.taxRate')}>{percent(po.taxRate)}</span></td><td className="l">Tax amount</td><td className={`v ${cls('tax.taxTotal')}`}>{amount(po.taxTotal)} {text(po.currency)}</td></tr>
                  <tr><td className="l"></td><td className="v"></td><td className="l">Gross value</td><td className={`v ${cls('total.total')}`}>{amount(po.total)} {text(po.currency)}</td></tr>
                </tbody>
              </table>
            </div>
            <div className="sap__status">Standard PO {text(po.poNumber)} displayed &nbsp;·&nbsp; ME23N</div>
          </div>
        )}
      </div>
    </section>
  );
}
