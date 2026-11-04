import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent } from 'react';
import { Theme, MessageSeverity } from '@uipath/coded-action-app';
import { PdfViewer } from '@uipath/ui-widgets-pdf-viewer';
import '@uipath/ui-widgets-pdf-viewer/PdfViewer.css';
import type { Invoice, MatchResult, PurchaseOrder, FormData } from '../types';
import { defaultFormData, normaliseMatchResult, parseJson } from '../types';
import { buildHighlights } from '../highlight';
import { actionApp, getSdk, loadPurchaseOrder, resolveBucket, reviewerEmail, message, MOCK } from '../platform';
import type { BucketRef } from '../platform';
import InvoicePane from './InvoicePane';
import PurchaseOrderPane from './PurchaseOrderPane';
import FindingsPanel from './FindingsPanel';
import EmailEditor from './EmailEditor';
import './Form.css';
import './Review.css';

const isDarkTheme = (theme: Theme): boolean => theme === Theme.Dark || theme === Theme.DarkHighContrast;

interface FormProps {
  onInitTheme: (isDark: boolean) => void;
  darkTheme: boolean;
  onToggleTheme: () => void;
}

type Tab = 'compare' | 'pdf' | 'page';

const STATUS_TEXT: Record<string, string> = {
  green: 'Green · pays',
  yellow: 'Yellow · presentation difference',
  red: 'Red · real discrepancy',
  review: 'Needs review',
};

/** Task data comes back from Action Center with its own key casing (POID is stored as Poid): match keys to the schema's names case-insensitively. */
function matchFormKeys(data: Record<string, unknown>): Partial<FormData> {
  const names = Object.keys(defaultFormData);
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(data)) out[names.find((n) => n.toLowerCase() === k.toLowerCase()) ?? k] = v;
  return out as Partial<FormData>;
}

function Form({ onInitTheme, darkTheme, onToggleTheme }: FormProps) {
  const [formData, setFormData] = useState<FormData>(defaultFormData);
  const [isReadOnly, setIsReadOnly] = useState(false);
  const [taskFolderId, setTaskFolderId] = useState<number | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [po, setPo] = useState<PurchaseOrder | null>(null);
  const [poHtml, setPoHtml] = useState<string | null>(null);
  const [poLoading, setPoLoading] = useState(false);
  const [poError, setPoError] = useState<string | null>(null);

  const [bucket, setBucket] = useState<BucketRef | null>(null);
  const [pdfError, setPdfError] = useState<string | null>(null);

  const [tab, setTab] = useState<Tab>('compare');
  const [selected, setSelected] = useState<number | null>(null);
  const [emailOpen, setEmailOpen] = useState(false);
  const [submitting, setSubmitting] = useState<string | null>(null);

  // 1. The task: merge over defaults (outputs are absent on first load), seed the theme, prefill the e-mail draft.
  useEffect(() => {
    actionApp.getTask().then((task) => {
      const incoming = matchFormKeys((task.data ?? {}) as Record<string, unknown>);
      const merged: FormData = { ...defaultFormData, ...incoming };
      if (!merged.RejectionEmailHtml) {
        const mr = parseJson<Partial<MatchResult>>(merged.MatchResultJSON).value;
        if (mr?.rejectionEmailHtml) merged.RejectionEmailHtml = String(mr.rejectionEmailHtml);
      }
      setFormData(merged);
      setIsReadOnly(task.isReadOnly);
      setTaskFolderId(task.folderId);
      onInitTheme(isDarkTheme(task.theme));
      if (merged.RejectionEmailHtml !== (incoming.RejectionEmailHtml ?? '')) actionApp.setTaskData(merged);
      // The reviewer: the assignee of the task, written into ReviewerEmail for the process.
      if (!merged.ReviewerEmail) {
        reviewerEmail(task.taskId, task.folderId).then((email) => {
          if (!email) return;
          setFormData((prev) => {
            const next = { ...prev, ReviewerEmail: email };
            actionApp.setTaskData(next);
            return next;
          });
        });
      }
    }).catch((e) => setLoadError(`The task could not be read from Action Center: ${message(e)}`));
  }, [onInitTheme]);

  // 2. The purchase order from Data Fabric.
  useEffect(() => {
    if (!formData.POID) return;
    let cancelled = false;
    setPoLoading(true);
    loadPurchaseOrder(formData.POID).then((r) => {
      if (cancelled) return;
      setPo(r.po); setPoHtml(r.html); setPoError(r.error); setPoLoading(false);
    });
    return () => { cancelled = true; };
  }, [formData.POID]);

  // 3. The invoice PDF's bucket.
  useEffect(() => {
    if (!formData.InvoiceFileName || taskFolderId == null) return;
    let cancelled = false;
    resolveBucket(taskFolderId).then((ref) => {
      if (cancelled) return;
      if (ref) setBucket(ref);
      else setPdfError(MOCK ? 'The PDF preview needs Action Center (local sample mode).' : 'Bucket InvoicesStorage was not found.');
    }).catch((e) => !cancelled && setPdfError(`The invoice PDF could not be located: ${message(e)}`));
    return () => { cancelled = true; };
  }, [formData.InvoiceFileName, taskFolderId]);

  const invoiceParsed = useMemo(() => parseJson<Invoice>(formData.InvoiceJSON), [formData.InvoiceJSON]);
  const resultParsed = useMemo(() => {
    const p = parseJson<Partial<MatchResult>>(formData.MatchResultJSON);
    return { value: p.value ? normaliseMatchResult(p.value) : null, error: p.error };
  }, [formData.MatchResultJSON]);
  const result = resultParsed.value;
  const invoiceHighlights = useMemo(() => buildHighlights(result?.findings ?? [], 'invoice'), [result]);
  const poHighlights = useMemo(() => buildHighlights(result?.findings ?? [], 'po'), [result]);
  const status = result ? (['green', 'yellow', 'red'].includes(result.status) ? result.status : 'review') : 'review';
  const invoice = invoiceParsed.value;
  const sdk = getSdk();

  const update = (patch: Partial<FormData>) => {
    if (isReadOnly) return;
    const updated = { ...formData, ...patch };
    setFormData(updated);
    actionApp.setTaskData(updated);
  };
  const handleTextChange = (e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => update({ [e.target.name]: e.target.value } as Partial<FormData>);

  const complete = async (outcome: 'Approve' | 'Reject') => {
    if (isReadOnly || submitting) return;
    setSubmitting(outcome);
    try {
      const res = await actionApp.completeTask(outcome, formData);
      if (!res.success) {
        console.error('completeTask failed', res);
        actionApp.showMessage(`Could not complete the task: ${res.errorMessage ?? res.errorCode}`, MessageSeverity.Error);
      }
    } catch (e) {
      console.error('completeTask threw', e);
      actionApp.showMessage(`Could not complete the task: ${message(e)}`, MessageSeverity.Error);
    } finally {
      setSubmitting(null);
    }
  };

  const canReject = !isReadOnly && !submitting && formData.RejectionEmailHtml.trim() !== '';
  const canApprove = !isReadOnly && !submitting;

  return (
    <div className="review-app screen">
      <header className="review-header screen__header">
        <div className="review-header__icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <path d="M14 2v6h6" />
            <path d="M9 15l2 2 4-4" />
          </svg>
        </div>
        <div className="review-header__titles">
          <h1 className="review-header__title">Invoice Validation</h1>
          <p className="review-header__subtitle">{invoice?.invoiceNumber || formData.InvoiceFileName || 'Invoice'} against {formData.POID || 'its purchase order'}{result?.summary ? ` · ${result.summary}` : ''}</p>
        </div>
        <div className="review-header__actions">
          <span className={`status status--${status}`}>{STATUS_TEXT[status]}</span>
          {isReadOnly && <span className="review-badge">Read only</span>}
          <button type="button" className="theme-toggle" onClick={onToggleTheme} aria-label={darkTheme ? 'Switch to light mode' : 'Switch to dark mode'} title={darkTheme ? 'Switch to light mode' : 'Switch to dark mode'}>
            {darkTheme ? (
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="4" />
                <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
              </svg>
            )}
          </button>
        </div>
      </header>

      <nav className="toptabs" role="tablist" aria-label="Views">
        <button type="button" role="tab" aria-selected={tab === 'compare'} className={`toptab${tab === 'compare' ? ' toptab--on' : ''}`} onClick={() => setTab('compare')}>Compare</button>
        <button type="button" role="tab" aria-selected={tab === 'pdf'} className={`toptab${tab === 'pdf' ? ' toptab--on' : ''}`} onClick={() => setTab('pdf')}>Invoice PDF</button>
        <button type="button" role="tab" aria-selected={tab === 'page'} className={`toptab${tab === 'page' ? ' toptab--on' : ''}`} onClick={() => setTab('page')}>Purchase order page</button>
        {MOCK && <span className="mock-pill" title="The task, the invoice and the purchase order come from samples/; Action Center and the platform are not connected.">local sample</span>}
      </nav>

      <main className="screen__main">
        {loadError && <div className="pane__error">{loadError}</div>}

        {tab === 'compare' && (
          <div className="compare">
            <FindingsPanel result={result} error={resultParsed.error} selected={selected} onSelect={setSelected} />
            <div className="panes">
              <InvoicePane invoice={invoice} invoiceError={invoiceParsed.error} highlights={invoiceHighlights} selected={selected} />
              <PurchaseOrderPane poid={formData.POID} po={po} loading={poLoading} error={poError} highlights={poHighlights} selected={selected} />
            </div>
          </div>
        )}

        {tab === 'pdf' && (
          <div className="fill">
            {bucket && sdk ? (
              <PdfViewer sdk={sdk} source={{ ...bucket, path: formData.InvoiceFileName }} fileName={formData.InvoiceFileName || 'invoice.pdf'} maxHeight="100%" />
            ) : (
              <div className="pane__empty">{pdfError ?? (formData.InvoiceFileName ? `Locating ${formData.InvoiceFileName} in InvoicesStorage…` : 'No invoice file name was passed to the task.')}</div>
            )}
          </div>
        )}

        {tab === 'page' && (
          <div className="fill">
            {poHtml ? (
              <iframe title={`Purchase order ${formData.POID}`} className="po-frame" sandbox="" srcDoc={poHtml} />
            ) : (
              <div className="pane__empty">{poLoading ? 'Loading the purchase order page…' : poError ?? 'The purchase order page (POHTML) is not available.'}</div>
            )}
          </div>
        )}
      </main>

      <footer className="decision">
        <div className="decision__field decision__field--grow">
          <label htmlFor="ReviewerComment">Comment</label>
          <input id="ReviewerComment" name="ReviewerComment" placeholder="Optional note for the payments team…" value={formData.ReviewerComment} onChange={handleTextChange} readOnly={isReadOnly} />
        </div>
        <div className="decision__field">
          <label htmlFor="ReviewerEmail">Reviewer</label>
          <input id="ReviewerEmail" name="ReviewerEmail" readOnly value={formData.ReviewerEmail || 'resolving…'} title="Written into PaymentsQueue.ApprovedBy when you approve" />
        </div>
        <button type="button" className="outcome-btn outcome-btn--ghost" onClick={() => setEmailOpen(true)} title="The e-mail the supplier receives if you reject">
          {formData.RejectionEmailHtml.trim() ? 'Supplier e-mail' : 'Write supplier e-mail'}
        </button>
        <button type="button" className="outcome-btn outcome-btn--secondary" onClick={() => complete('Reject')} disabled={!canReject} title={canReject ? 'E-mail the supplier and stop the payment' : 'Write the supplier e-mail first'}>
          {submitting === 'Reject' ? 'Rejecting…' : 'Reject'}
        </button>
        <button type="button" className="outcome-btn outcome-btn--primary" onClick={() => complete('Approve')} disabled={!canApprove}>
          {submitting === 'Approve' ? 'Approving…' : 'Approve payment'}
        </button>
      </footer>

      {emailOpen && (
        <div className="drawer-backdrop" onClick={() => setEmailOpen(false)}>
          <aside className="drawer" role="dialog" aria-label="E-mail to the supplier" onClick={(e) => e.stopPropagation()}>
            <EmailEditor value={formData.RejectionEmailHtml} readOnly={isReadOnly} onChange={(html) => update({ RejectionEmailHtml: html })} />
            <div className="drawer__foot">
              <span className="muted small">The process sends this e-mail only when you reject.</span>
              <button type="button" className="outcome-btn outcome-btn--primary" onClick={() => setEmailOpen(false)}>Done</button>
            </div>
          </aside>
        </div>
      )}
    </div>
  );
}

export default Form;
