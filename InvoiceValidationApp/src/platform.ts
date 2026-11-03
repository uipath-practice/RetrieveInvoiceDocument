import { UiPath } from '@uipath/uipath-typescript/core';
import { Entities, QueryFilterOperator } from '@uipath/uipath-typescript/entities';
import { Buckets } from '@uipath/uipath-typescript/buckets';
import { Tasks } from '@uipath/uipath-typescript/tasks';
import { CodedActionAppService, Theme, TaskStatus } from '@uipath/coded-action-app';
import type { Task, MessageSeverity } from '@uipath/coded-action-app';
import type { PurchaseOrder } from './types';
import { parseJson } from './types';

/**
 * Local development without Action Center: `npm run dev` opens the app with the sample payloads in samples/
 * (a real generator run, INV-88757045 against PO-98476545). Action Center always passes `basedomain`.
 */
export const MOCK = import.meta.env.DEV && !new URLSearchParams(window.location.search).has('basedomain');

export const ENTITY = 'PurchaseOrders';
export const BUCKET = 'InvoicesStorage';

// Never call sdk.initialize() in an action app: Action Center injects the session (meta tags); initialize() starts a redirect.
let sdkInstance: UiPath | null = null;
export function getSdk(): UiPath | null {
  if (MOCK) return null;
  if (!sdkInstance) {
    try {
      sdkInstance = new UiPath();
    } catch (e) {
      console.warn('UiPath SDK could not start', e);
      return null;
    }
  }
  return sdkInstance;
}

/** The folder the app is deployed in (Tools): the platform injects it at deploy time. */
export function deployFolderKey(): string | null {
  const meta = document.querySelector('meta[name="uipath:folder-key"]') as HTMLMetaElement | null;
  return meta?.content || null;
}

// ---------------------------------------------------------------------------------------------------------------------
// Action Center

const real = MOCK ? null : new CodedActionAppService();

export const actionApp = {
  async getTask(): Promise<Task> {
    if (real) return real.getTask();
    const [inv, mr] = await Promise.all([
      import('../samples/invoice.json'),
      import('../samples/match-result.json'),
    ]);
    return {
      taskId: 0,
      title: 'Invoice INV-88757045 — review (local sample)',
      status: TaskStatus.Pending,
      isReadOnly: false,
      action: null,
      data: {
        InvoiceFileName: 'INV-88757045.pdf',
        InvoiceJSON: JSON.stringify(inv.default),
        POID: 'PO-98476545',
        MatchResultJSON: JSON.stringify(mr.default),
      },
      folderId: 0,
      folderName: 'Local',
      jobKey: null,
      theme: Theme.Light,
    };
  },
  setTaskData(data: unknown): void {
    if (real) real.setTaskData(data);
    else console.debug('[mock] setTaskData', data);
  },
  showMessage(msg: string, severity: MessageSeverity): void {
    if (real) real.showMessage(msg, severity);
    else console.info(`[mock] ${severity}: ${msg}`);
  },
  async completeTask(action: string, data: unknown) {
    if (real) return real.completeTask(action, data);
    console.info('[mock] completeTask', action, data);
    window.alert(`${action}\n\n${JSON.stringify(data, null, 2)}`);
    return { success: true, errorCode: null, errorMessage: null };
  },
};

// ---------------------------------------------------------------------------------------------------------------------
// Data Fabric: the PurchaseOrders row for a POID

export interface PurchaseOrderLoad {
  po: PurchaseOrder | null;
  html: string | null;
  error: string | null;
}

export async function loadPurchaseOrder(poid: string): Promise<PurchaseOrderLoad> {
  if (!poid) return { po: null, html: null, error: null };
  if (MOCK) {
    const sample = (await import('../samples/purchase-order.json')).default as PurchaseOrder;
    return { po: sample.poNumber === poid ? sample : null, html: null, error: null };
  }
  const sdk = getSdk();
  if (!sdk) return { po: null, html: null, error: 'The UiPath SDK is not available in this page.' };
  const entities = new Entities(sdk);
  try {
    const page = await entities.queryRecords({ name: ENTITY }, {
      filterGroup: { queryFilters: [{ fieldName: 'POID', operator: QueryFilterOperator.Equals, value: poid }] },
      pageSize: 1,
    });
    const record = page.items?.[0];
    if (!record) return { po: null, html: null, error: null };
    const data = parseJson<PurchaseOrder>(pick(record, 'PODATA'));
    let html: string | null = null;
    try {
      const blob = await entities.downloadAttachment({ name: ENTITY }, record.Id, 'POHTML');
      html = await blob.text();
    } catch (e) {
      console.warn('POHTML download failed', e);
    }
    return { po: data.value, html, error: data.error ? `PODATA is not valid JSON: ${data.error}` : null };
  } catch (e) {
    return { po: null, html: null, error: `Purchase order ${poid} could not be loaded from Data Fabric: ${message(e)}` };
  }
}

/** Data Fabric returns field names in its own casing (POID arrives as Poid); read them case-insensitively. */
function pick(record: Record<string, unknown>, field: string): unknown {
  const key = Object.keys(record).find((k) => k.toLowerCase() === field.toLowerCase());
  return key ? record[key] : undefined;
}

// ---------------------------------------------------------------------------------------------------------------------
// Orchestrator: the invoice PDF's bucket and the reviewer

export interface BucketRef {
  bucketId: number;
  folderKey?: string;
  folderId?: number;
}

export async function resolveBucket(taskFolderId: number): Promise<BucketRef | null> {
  if (MOCK) return null;
  const sdk = getSdk();
  if (!sdk) return null;
  const folderKey = deployFolderKey();
  // The bucket lives in the app's own folder (Tools); the task may sit in the participant's workspace.
  const bucket = await new Buckets(sdk).getByName(BUCKET, folderKey ? { folderKey } : { folderId: taskFolderId });
  return folderKey ? { bucketId: bucket.id, folderKey } : { bucketId: bucket.id, folderId: taskFolderId };
}

export async function reviewerEmail(taskId: number, folderId: number): Promise<string> {
  if (MOCK) return 'reviewer@example.com';
  const sdk = getSdk();
  if (!sdk) return '';
  try {
    const task = await new Tasks(sdk).getById(taskId, {}, folderId);
    return task.assignedToUser?.emailAddress ?? task.assignedToUser?.userName ?? '';
  } catch (e) {
    console.warn('Task assignee lookup failed', e);
    return '';
  }
}

export function message(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}
