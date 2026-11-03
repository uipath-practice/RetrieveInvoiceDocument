import { useState } from 'react';
import type { ChangeEvent } from 'react';

interface Props {
  value: string;
  readOnly: boolean;
  onChange: (html: string) => void;
}

/** The supplier e-mail the agent drafted: edit the HTML or preview it. The process sends it only on Reject. */
export default function EmailEditor({ value, readOnly, onChange }: Props) {
  const [preview, setPreview] = useState(true);
  const handle = (e: ChangeEvent<HTMLTextAreaElement>) => onChange(e.target.value);

  return (
    <div className="email">
      <div className="email__bar">
        <label htmlFor="RejectionEmailHtml">E-mail to the supplier <span className="email__hint">sent on Reject only</span></label>
        <div className="tabs" role="tablist">
          <button type="button" role="tab" aria-selected={preview} className={`tab${preview ? ' tab--on' : ''}`} onClick={() => setPreview(true)}>Preview</button>
          <button type="button" role="tab" aria-selected={!preview} className={`tab${!preview ? ' tab--on' : ''}`} onClick={() => setPreview(false)}>Edit HTML</button>
        </div>
      </div>
      {preview ? (
        <div className="email__preview" dangerouslySetInnerHTML={{ __html: sanitise(value) || '<p class="email__none">No e-mail was drafted (nothing to send on a green match).</p>' }} />
      ) : (
        <textarea id="RejectionEmailHtml" name="RejectionEmailHtml" rows={10} value={value} onChange={handle} readOnly={readOnly} spellCheck={false} />
      )}
    </div>
  );
}

/** Enough for a preview of our own agent's draft: drop scripts, styles and inline handlers. */
function sanitise(html: string): string {
  return (html || '')
    .replace(/<\s*(script|style|iframe|object|embed)[^>]*>[\s\S]*?<\s*\/\s*\1\s*>/gi, '')
    .replace(/\son\w+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi, '')
    .replace(/javascript:/gi, '');
}
