import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { KeyRound, Plug } from 'lucide-react';
import { apiFetch } from '../../lib/api';
import { useAuth } from '../../app/AuthContext';

interface TenantPluginItem {
  pluginId: string;
  name: string;
  description: string;
  version: string;
  isGloballyEnabled: boolean;
  defaultPrice: number;
  status: string;
  isEnabledByTenant: boolean;
  expiresAt?: string;
  rejectionReason?: string;
  invoice?: { invoiceNumber: string; amount: number; currency: string; status: string } | null;
  license?: { licenseKey: string; durationType: string } | null;
}

export function TenantPluginsPage() {
  const qc = useQueryClient();
  const { refreshAuth } = useAuth();
  const [licenseKey, setLicenseKey] = useState('');
  const [feedback, setFeedback] = useState<{ type: 'ok' | 'err'; msg: string } | null>(null);

  const { data: plugins = [] } = useQuery({
    queryKey: ['tenant-plugins'],
    queryFn: () => apiFetch<TenantPluginItem[]>('/api/v1/tenant/plugins'),
  });

  const redeem = useMutation({
    mutationFn: () =>
      apiFetch<{ invoice: { invoiceNumber: string } }>('/api/v1/tenant/plugins/redeem', {
        method: 'POST',
        body: JSON.stringify({ licenseKey }),
      }),
    onSuccess: (res) => {
      setLicenseKey('');
      setFeedback({
        type: 'ok',
        msg: `License submitted! Paid invoice ${res.invoice.invoiceNumber} created. Awaiting Platform Admin approval.`,
      });
      qc.invalidateQueries({ queryKey: ['tenant-plugins'] });
    },
    onError: (err: any) => setFeedback({ type: 'err', msg: err.message }),
  });

  const toggle = useMutation({
    mutationFn: ({ pluginId, enabled }: { pluginId: string; enabled: boolean }) =>
      apiFetch(`/api/v1/tenant/plugins/${pluginId}/toggle`, {
        method: 'PATCH',
        body: JSON.stringify({ enabled }),
      }),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['tenant-plugins'] });
      await refreshAuth();
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Plugins &amp; Add-ons</h1>
        <p className="text-sm text-slate-500">
          All plugins are pre-installed on the platform. Enable licensed modules or redeem a license number.
        </p>
      </div>

      {/* Redeem Banner */}
      <div className="rounded-card border border-indigo-200 bg-indigo-50/70 p-5 flex flex-wrap items-center justify-between gap-4">
        <div className="max-w-xl">
          <div className="flex items-center gap-2 text-base font-semibold text-slate-900">
            <KeyRound className="h-4 w-4 text-primary" />
            Redeem Plugin License Number
          </div>
          <p className="mt-1 text-xs text-slate-600">
            Submitting a license number generates a paid invoice and sends an activation request for Platform Admin approval.
          </p>
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setFeedback(null);
            if (licenseKey.trim()) redeem.mutate();
          }}
          className="flex items-center gap-2"
        >
          <input
            type="text"
            placeholder="PLG-REF-2026-XXXX-XXXX"
            value={licenseKey}
            onChange={(e) => setLicenseKey(e.target.value)}
            className="w-64 rounded-control border border-indigo-200 bg-white px-3 py-2 text-sm font-mono focus:border-primary focus:outline-none"
          />
          <button
            type="submit"
            disabled={redeem.isPending}
            className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white hover:opacity-95"
          >
            Submit Request
          </button>
        </form>
      </div>

      {feedback && (
        <div
          className={`rounded-control border px-4 py-2.5 text-sm ${
            feedback.type === 'ok'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {feedback.msg}
        </div>
      )}

      {/* 3-Column Plugin Cards */}
      {plugins.length === 0 ? (
        <div className="rounded-card border border-slate-200 bg-surface p-8 text-center text-sm text-slate-400">
          Belum ada plugin terpasang di katalog platform.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
          {plugins.map((p) => {
            const isActive = p.status === 'Active';
            const badge = getStatusBadge(p.status);
            return (
              <div key={p.pluginId} className="flex flex-col justify-between rounded-card border border-slate-200 bg-surface p-5">
                <div>
                  <div className="flex items-center justify-between">
                    <span className={`rounded px-2 py-0.5 text-[11px] font-bold uppercase ${badge.cls}`}>
                      {badge.label}
                    </span>
                    <button
                      type="button"
                      disabled={!isActive || toggle.isPending}
                      onClick={() => toggle.mutate({ pluginId: p.pluginId, enabled: !p.isEnabledByTenant })}
                      className={`relative inline-flex h-6 w-11 items-center rounded-full transition ${
                        isActive && p.isEnabledByTenant ? 'bg-primary' : 'bg-slate-200'
                      } ${!isActive ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
                      title={isActive ? 'Toggle plugin visibility' : 'Requires active license'}
                    >
                      <span
                        className={`inline-block h-4 w-4 transform rounded-full bg-white transition ${
                          isActive && p.isEnabledByTenant ? 'translate-x-6' : 'translate-x-1'
                        }`}
                      />
                    </button>
                  </div>

                  <div className="mt-4 flex items-center gap-2">
                    <Plug className="h-4 w-4 text-primary" />
                    <h3 className="text-base font-bold text-slate-900">{p.name}</h3>
                  </div>
                  <p className="mt-1.5 text-xs text-slate-500 leading-relaxed">{p.description}</p>
                </div>

                <div className="mt-4 border-t border-slate-100 pt-3 text-xs text-slate-500">
                  {p.status === 'Active' && p.license && (
                    <span>{p.license.licenseKey} • {p.license.durationType}</span>
                  )}
                  {p.status === 'PendingApproval' && p.invoice && (
                    <span className="text-amber-700 font-medium">{p.invoice.invoiceNumber} (Paid) — Awaiting Admin</span>
                  )}
                  {p.status === 'Rejected' && (
                    <span className="text-red-600">Rejected: {p.rejectionReason}</span>
                  )}
                  {(p.status === 'NotLicensed' || p.status === 'Disabled' || p.status === 'Expired') && (
                    <span>Requires license number • v{p.version}</span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function getStatusBadge(status: string) {
  switch (status) {
    case 'Active':
      return { label: 'ACTIVE', cls: 'bg-emerald-100 text-emerald-800' };
    case 'PendingApproval':
      return { label: 'PENDING APPROVAL', cls: 'bg-amber-100 text-amber-800' };
    case 'Rejected':
      return { label: 'REJECTED', cls: 'bg-red-100 text-red-700' };
    case 'Expired':
      return { label: 'EXPIRED', cls: 'bg-red-100 text-red-700' };
    default:
      return { label: 'NOT LICENSED', cls: 'bg-slate-100 text-slate-600' };
  }
}
