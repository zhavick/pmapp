import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '../../lib/api';

interface LicenseReq {
  id: string;
  tenantName: string;
  tenantSlug: string;
  pluginId: string;
  pluginName: string;
  status: string;
  licenseKey: string;
  durationType: string;
  invoiceNumber: string;
  invoiceAmount: number;
  invoiceCurrency: string;
  invoiceStatus: string;
  requestedBy: string;
  requestedAt: string;
  rejectionReason?: string;
}

export function PlatformLicenseRequestsPage() {
  const qc = useQueryClient();
  const [tab, setTab] = useState<'PendingApproval' | 'Active' | 'Rejected'>('PendingApproval');
  const [selected, setSelected] = useState<LicenseReq | null>(null);
  const [reason, setReason] = useState('');

  const { data: requests = [] } = useQuery({
    queryKey: ['platform-requests', tab],
    queryFn: () => apiFetch<LicenseReq[]>(`/api/v1/platform/license-requests?status=${tab}`),
  });

  const approve = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/v1/platform/license-requests/${id}/approve`, { method: 'POST' }),
    onSuccess: () => {
      setSelected(null);
      qc.invalidateQueries({ queryKey: ['platform-requests'] });
    },
  });

  const reject = useMutation({
    mutationFn: (id: string) =>
      apiFetch(`/api/v1/platform/license-requests/${id}/reject`, {
        method: 'POST',
        body: JSON.stringify({ reason }),
      }),
    onSuccess: () => {
      setReason('');
      setSelected(null);
      qc.invalidateQueries({ queryKey: ['platform-requests'] });
    },
  });

  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-bold text-white">License Requests</h1>

      <div className="flex gap-6 border-b border-slate-800 text-sm">
        {[
          { key: 'PendingApproval', label: 'Pending' },
          { key: 'Active', label: 'Approved' },
          { key: 'Rejected', label: 'Rejected' },
        ].map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => {
              setTab(t.key as any);
              setSelected(null);
            }}
            className={`pb-2.5 font-medium ${
              tab === t.key ? 'border-b-2 border-teal-400 text-teal-300' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-xl border border-slate-800 bg-[#161E2E] overflow-hidden">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-800 text-xs uppercase text-slate-400">
              <tr>
                <th className="px-4 py-3">Tenant</th>
                <th className="px-4 py-3">Plugin</th>
                <th className="px-4 py-3">License No.</th>
                <th className="px-4 py-3">Invoice</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/70">
              {requests.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-slate-500">No requests in this queue.</td>
                </tr>
              ) : (
                requests.map((r) => (
                  <tr
                    key={r.id}
                    onClick={() => setSelected(r)}
                    className={`cursor-pointer hover:bg-slate-800/40 ${selected?.id === r.id ? 'bg-teal-950/30' : ''}`}
                  >
                    <td className="px-4 py-3 font-medium text-white">{r.tenantName}</td>
                    <td className="px-4 py-3 text-slate-300">{r.pluginName}</td>
                    <td className="px-4 py-3 font-mono text-xs text-slate-300">{r.licenseKey}</td>
                    <td className="px-4 py-3">
                      <div className="text-xs text-slate-300">{r.invoiceNumber}</div>
                      <span className="rounded bg-emerald-500/20 px-1.5 py-0.5 text-[10px] font-bold text-emerald-300">
                        {r.invoiceStatus}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                      {tab === 'PendingApproval' && (
                        <button
                          type="button"
                          onClick={() => approve.mutate(r.id)}
                          className="rounded bg-teal-500 px-2.5 py-1 text-xs font-semibold text-slate-950 hover:bg-teal-400"
                        >
                          Approve
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Request Details Side Panel */}
        <div className="rounded-xl border border-slate-800 bg-[#161E2E] p-5 space-y-4">
          <h2 className="text-base font-semibold text-white">Request Details</h2>
          {!selected ? (
            <p className="text-xs text-slate-500">Select a row to inspect invoice &amp; approve/reject.</p>
          ) : (
            <div className="space-y-4 text-xs">
              <div className="rounded-lg border border-slate-800 bg-slate-900/60 p-3 space-y-1">
                <div className="text-slate-400">Tenant Info</div>
                <div className="text-sm font-semibold text-white">{selected.tenantName}</div>
                <div className="text-slate-400">Requested by: {selected.requestedBy}</div>
              </div>
              <div className="rounded-lg border border-slate-800 bg-slate-900/60 p-3 space-y-1">
                <div className="text-slate-400">Plugin &amp; License</div>
                <div className="text-sm font-semibold text-white">{selected.pluginName}</div>
                <div className="font-mono text-teal-300">{selected.licenseKey}</div>
                <div className="text-slate-400">Validity: {selected.durationType}</div>
              </div>
              <div className="rounded-lg border border-slate-800 bg-slate-900/60 p-3 space-y-1">
                <div className="text-slate-400">Invoice Summary</div>
                <div className="flex justify-between font-semibold text-white">
                  <span>{selected.invoiceNumber}</span>
                  <span>{selected.invoiceCurrency} {selected.invoiceAmount}</span>
                </div>
              </div>
              {tab === 'PendingApproval' && (
                <div className="space-y-3">
                  <textarea
                    rows={3}
                    placeholder="Reason (required for reject)..."
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    className="w-full rounded-lg border border-slate-700 bg-slate-900 p-2.5 text-xs text-white focus:border-teal-400 focus:outline-none"
                  />
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      type="button"
                      onClick={() => approve.mutate(selected.id)}
                      className="rounded-lg bg-teal-500 py-2 font-semibold text-slate-950 hover:bg-teal-400"
                    >
                      Approve
                    </button>
                    <button
                      type="button"
                      disabled={!reason.trim()}
                      onClick={() => reject.mutate(selected.id)}
                      className="rounded-lg border border-red-500/60 py-2 font-semibold text-red-400 hover:bg-red-950/40 disabled:opacity-40"
                    >
                      Reject
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export function PlatformLicensesPage() {
  const qc = useQueryClient();
  const [form, setForm] = useState({ pluginId: 'ref', durationType: 'OneYear', invoiceAmount: 99, currency: 'USD' });

  const { data: plugins = [] } = useQuery({
    queryKey: ['platform-plugins'],
    queryFn: () => apiFetch<{ pluginId: string; name: string }[]>('/api/v1/platform/plugins'),
  });

  const { data: licenses = [] } = useQuery({
    queryKey: ['platform-licenses'],
    queryFn: () => apiFetch<{ id: string; licenseKey: string; pluginId: string; durationType: string; invoiceAmount: number; status: string }[]>('/api/v1/platform/licenses'),
  });

  const generate = useMutation({
    mutationFn: () => apiFetch('/api/v1/platform/licenses/generate', { method: 'POST', body: JSON.stringify(form) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['platform-licenses'] }),
  });

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-white">License Numbers Generator</h1>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          generate.mutate();
        }}
        className="flex flex-wrap items-end gap-3 rounded-xl border border-slate-800 bg-[#161E2E] p-4"
      >
        <div>
          <label className="block text-xs text-slate-400 mb-1">Plugin</label>
          <select
            value={form.pluginId}
            onChange={(e) => setForm({ ...form, pluginId: e.target.value })}
            className="rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-white"
          >
            {plugins.map((p) => <option key={p.pluginId} value={p.pluginId}>{p.name}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-xs text-slate-400 mb-1">Duration</label>
          <select
            value={form.durationType}
            onChange={(e) => setForm({ ...form, durationType: e.target.value })}
            className="rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-white"
          >
            <option value="OneMonth">1 Month</option>
            <option value="OneYear">1 Year</option>
            <option value="Lifetime">Lifetime</option>
          </select>
        </div>
        <div>
          <label className="block text-xs text-slate-400 mb-1">Invoice Amount</label>
          <input
            type="number"
            value={form.invoiceAmount}
            onChange={(e) => setForm({ ...form, invoiceAmount: Number(e.target.value) })}
            className="w-32 rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-white"
          />
        </div>
        <button type="submit" className="rounded-lg bg-teal-500 px-4 py-2 text-sm font-semibold text-slate-950">
          Generate License
        </button>
      </form>

      <div className="rounded-xl border border-slate-800 bg-[#161E2E] overflow-hidden">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-800 text-xs uppercase text-slate-400">
            <tr>
              <th className="px-4 py-3">License Key</th>
              <th className="px-4 py-3">Plugin</th>
              <th className="px-4 py-3">Duration</th>
              <th className="px-4 py-3">Amount</th>
              <th className="px-4 py-3">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/70">
            {licenses.map((l) => (
              <tr key={l.id}>
                <td className="px-4 py-3 font-mono text-xs text-teal-300">{l.licenseKey}</td>
                <td className="px-4 py-3 text-slate-300">{l.pluginId}</td>
                <td className="px-4 py-3 text-slate-300">{l.durationType}</td>
                <td className="px-4 py-3 text-slate-300">${l.invoiceAmount}</td>
                <td className="px-4 py-3">
                  <span className="rounded bg-slate-800 px-2 py-0.5 text-xs text-slate-200">{l.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function PlatformTenantsPage() {
  const { data: tenants = [] } = useQuery({
    queryKey: ['platform-tenants'],
    queryFn: () => apiFetch<{ id: string; companyName: string; slug: string; status: string; usersCount: number; maxUsersQuota: number }[]>('/api/v1/platform/tenants'),
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold text-white">Tenants</h1>
      <div className="rounded-xl border border-slate-800 bg-[#161E2E] overflow-hidden">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-800 text-xs uppercase text-slate-400">
            <tr>
              <th className="px-4 py-3">Company</th>
              <th className="px-4 py-3">Slug</th>
              <th className="px-4 py-3">Users</th>
              <th className="px-4 py-3">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/70">
            {tenants.map((t) => (
              <tr key={t.id}>
                <td className="px-4 py-3 font-medium text-white">{t.companyName}</td>
                <td className="px-4 py-3 text-slate-400">{t.slug}</td>
                <td className="px-4 py-3 text-slate-300">{t.usersCount} / {t.maxUsersQuota}</td>
                <td className="px-4 py-3"><span className="rounded bg-emerald-500/20 px-2 py-0.5 text-xs text-emerald-300">{t.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function PlatformPluginsPage() {
  const qc = useQueryClient();
  const { data: plugins = [] } = useQuery({
    queryKey: ['platform-plugins'],
    queryFn: () => apiFetch<{ pluginId: string; name: string; version: string; isGloballyEnabled: boolean }[]>('/api/v1/platform/plugins'),
  });

  const toggleGlobal = useMutation({
    mutationFn: ({ pluginId, enabled }: { pluginId: string; enabled: boolean }) =>
      apiFetch(`/api/v1/platform/plugins/${pluginId}/global-toggle`, {
        method: 'PATCH',
        body: JSON.stringify({ isGloballyEnabled: enabled }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['platform-plugins'] }),
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold text-white">Plugin Catalog &amp; Global Kill Switch</h1>
      <div className="rounded-xl border border-slate-800 bg-[#161E2E] overflow-hidden">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-800 text-xs uppercase text-slate-400">
            <tr>
              <th className="px-4 py-3">Plugin</th>
              <th className="px-4 py-3">Version</th>
              <th className="px-4 py-3 text-right">Global Kill Switch</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/70">
            {plugins.map((p) => (
              <tr key={p.pluginId}>
                <td className="px-4 py-3 font-medium text-white">{p.name} ({p.pluginId})</td>
                <td className="px-4 py-3 text-slate-400">{p.version}</td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={() => toggleGlobal.mutate({ pluginId: p.pluginId, enabled: !p.isGloballyEnabled })}
                    className={`rounded px-3 py-1 text-xs font-semibold ${
                      p.isGloballyEnabled ? 'bg-emerald-500/20 text-emerald-300' : 'bg-red-500/20 text-red-300'
                    }`}
                  >
                    {p.isGloballyEnabled ? 'Globally Enabled' : 'Killed'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function PlatformInvoicesPage() {
  const { data: invoices = [] } = useQuery({
    queryKey: ['platform-invoices'],
    queryFn: () => apiFetch<{ id: string; invoiceNumber: string; tenantName: string; pluginId: string; amount: number; currency: string; status: string }[]>('/api/v1/platform/invoices'),
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold text-white">Platform Invoices</h1>
      <div className="rounded-xl border border-slate-800 bg-[#161E2E] overflow-hidden">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-800 text-xs uppercase text-slate-400">
            <tr>
              <th className="px-4 py-3">Invoice</th>
              <th className="px-4 py-3">Tenant</th>
              <th className="px-4 py-3">Plugin</th>
              <th className="px-4 py-3">Amount</th>
              <th className="px-4 py-3">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/70">
            {invoices.map((i) => (
              <tr key={i.id}>
                <td className="px-4 py-3 font-mono text-xs text-white">{i.invoiceNumber}</td>
                <td className="px-4 py-3 text-slate-300">{i.tenantName}</td>
                <td className="px-4 py-3 text-slate-300">{i.pluginId}</td>
                <td className="px-4 py-3 text-slate-300">{i.currency} {i.amount}</td>
                <td className="px-4 py-3"><span className="rounded bg-emerald-500/20 px-2 py-0.5 text-xs text-emerald-300">{i.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function PlatformAuditPage() {
  const { data: logs = [] } = useQuery({
    queryKey: ['platform-audit'],
    queryFn: () => apiFetch<{ id: string; action: string; entityName: string; entityId: string; createdAt: string }[]>('/api/v1/platform/audit-logs'),
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold text-white">Global Audit Log</h1>
      <div className="rounded-xl border border-slate-800 bg-[#161E2E] p-4 space-y-2 text-xs">
        {logs.map((l) => (
          <div key={l.id} className="flex justify-between border-b border-slate-800 pb-2 text-slate-300">
            <span><strong>{l.action}</strong> • {l.entityName} ({l.entityId})</span>
            <span className="text-slate-500">{new Date(l.createdAt).toLocaleString()}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
