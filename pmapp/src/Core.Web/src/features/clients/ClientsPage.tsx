import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, Trash2, UserPlus, X } from 'lucide-react';
import { apiFetch } from '../../lib/api';
import { useUndoToast } from '../../components/ui/UndoToast';
import { ExtensionSlot } from '../../extensions/ExtensionSlot';

interface ClientItem {
  id: string;
  companyName: string;
  email?: string;
  phone?: string;
  currency: string;
  website?: string;
}

interface ClientDetail extends ClientItem {
  address?: string;
  contacts: { id: string; fullName: string; email: string; phone?: string; jobTitle?: string; isPrimary: boolean }[];
  projects: { id: string; title: string; status: string; deadline?: string }[];
}

export function ClientsPage() {
  const qc = useQueryClient();
  const showUndo = useUndoToast();
  const [search, setSearch] = useState('');
  const [showAdd, setShowAdd] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'contacts' | 'projects'>('contacts');

  const [form, setForm] = useState({ companyName: '', email: '', phone: '', currency: 'IDR', website: '' });
  const [contactForm, setContactForm] = useState({ fullName: '', email: '', jobTitle: '' });

  const { data: clients = [] } = useQuery({
    queryKey: ['clients', search],
    queryFn: () => apiFetch<ClientItem[]>(`/api/v1/clients?search=${encodeURIComponent(search)}`),
  });

  const { data: detail } = useQuery({
    queryKey: ['client-detail', selectedId],
    queryFn: () => apiFetch<ClientDetail>(`/api/v1/clients/${selectedId}`),
    enabled: !!selectedId,
  });

  const createClient = useMutation({
    mutationFn: () => apiFetch('/api/v1/clients', { method: 'POST', body: JSON.stringify(form) }),
    onSuccess: () => {
      setShowAdd(false);
      setForm({ companyName: '', email: '', phone: '', currency: 'IDR', website: '' });
      qc.invalidateQueries({ queryKey: ['clients'] });
    },
  });

  const deleteClient = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/v1/clients/${id}`, { method: 'DELETE' }),
    onSuccess: (_, id) => {
      if (selectedId === id) setSelectedId(null);
      qc.invalidateQueries({ queryKey: ['clients'] });
      showUndo('Client deleted', async () => {
        await apiFetch(`/api/v1/clients/${id}/restore`, { method: 'POST' });
        qc.invalidateQueries({ queryKey: ['clients'] });
      });
    },
  });

  const addContact = useMutation({
    mutationFn: () =>
      apiFetch(`/api/v1/clients/${selectedId}/contacts`, {
        method: 'POST',
        body: JSON.stringify({ ...contactForm, isPrimary: false }),
      }),
    onSuccess: () => {
      setContactForm({ fullName: '', email: '', jobTitle: '' });
      qc.invalidateQueries({ queryKey: ['client-detail', selectedId] });
    },
  });

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Clients</h1>
          <p className="text-sm text-slate-500">Manage customer organizations, contacts, and currencies.</p>
        </div>
        <button
          type="button"
          onClick={() => setShowAdd(true)}
          className="inline-flex items-center gap-2 rounded-control bg-primary px-4 py-2 text-sm font-medium text-white hover:opacity-95"
        >
          <Plus className="h-4 w-4" />
          Add Client
        </button>
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search clients..."
          className="w-full rounded-control border border-slate-200 bg-surface py-2 pl-9 pr-3 text-sm focus:border-primary focus:outline-none"
        />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-card border border-slate-200 bg-surface overflow-hidden">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Company</th>
                <th className="px-4 py-3">Email</th>
                <th className="px-4 py-3">Currency</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {clients.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-slate-400">
                    Belum ada client. Klik "Add Client" untuk memulai.
                  </td>
                </tr>
              ) : (
                clients.map((c) => (
                  <tr
                    key={c.id}
                    onClick={() => setSelectedId(c.id)}
                    className={`cursor-pointer hover:bg-slate-50 ${selectedId === c.id ? 'bg-indigo-50/50' : ''}`}
                  >
                    <td className="px-4 py-3 font-medium text-slate-900">{c.companyName}</td>
                    <td className="px-4 py-3 text-slate-500">{c.email || '-'}</td>
                    <td className="px-4 py-3">
                      <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">{c.currency}</span>
                    </td>
                    <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                      <button
                        type="button"
                        onClick={() => deleteClient.mutate(c.id)}
                        className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600"
                        title="Delete"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Client Details Side Panel */}
        <div className="rounded-card border border-slate-200 bg-surface p-5">
          {!detail ? (
            <p className="text-sm text-slate-400">Pilih client untuk melihat kontak, proyek, dan tab plugin.</p>
          ) : (
            <div className="space-y-4">
              <div>
                <h2 className="text-lg font-bold text-slate-900">{detail.companyName}</h2>
                <p className="text-xs text-slate-500">{detail.email || 'No email'} • {detail.currency}</p>
              </div>

              <div className="flex gap-3 border-b border-slate-200 text-sm">
                <button
                  type="button"
                  onClick={() => setActiveTab('contacts')}
                  className={`pb-2 font-medium ${activeTab === 'contacts' ? 'border-b-2 border-primary text-primary' : 'text-slate-500'}`}
                >
                  Contacts ({detail.contacts.length})
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab('projects')}
                  className={`pb-2 font-medium ${activeTab === 'projects' ? 'border-b-2 border-primary text-primary' : 'text-slate-500'}`}
                >
                  Projects ({detail.projects.length})
                </button>
              </div>

              {activeTab === 'contacts' ? (
                <div className="space-y-3">
                  {detail.contacts.map((ct) => (
                    <div key={ct.id} className="rounded-control border border-slate-100 bg-slate-50 p-2.5 text-xs">
                      <div className="font-semibold text-slate-800">{ct.fullName}</div>
                      <div className="text-slate-500">{ct.email} {ct.jobTitle ? `• ${ct.jobTitle}` : ''}</div>
                    </div>
                  ))}
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      addContact.mutate();
                    }}
                    className="space-y-2 pt-2 border-t border-slate-100"
                  >
                    <input
                      type="text"
                      required
                      placeholder="Contact name"
                      value={contactForm.fullName}
                      onChange={(e) => setContactForm({ ...contactForm, fullName: e.target.value })}
                      className="w-full rounded-control border border-slate-200 px-2.5 py-1.5 text-xs"
                    />
                    <input
                      type="email"
                      required
                      placeholder="Contact email"
                      value={contactForm.email}
                      onChange={(e) => setContactForm({ ...contactForm, email: e.target.value })}
                      className="w-full rounded-control border border-slate-200 px-2.5 py-1.5 text-xs"
                    />
                    <button
                      type="submit"
                      className="inline-flex items-center gap-1 rounded-control bg-slate-800 px-3 py-1.5 text-xs font-medium text-white"
                    >
                      <UserPlus className="h-3.5 w-3.5" /> Add Contact
                    </button>
                  </form>
                </div>
              ) : (
                <div className="space-y-2">
                  {detail.projects.map((p) => (
                    <div key={p.id} className="rounded-control border border-slate-100 bg-slate-50 p-2.5 text-xs">
                      <div className="font-semibold text-slate-800">{p.title}</div>
                      <div className="text-slate-500">{p.status}</div>
                    </div>
                  ))}
                </div>
              )}

              <ExtensionSlot name="client.details.tabs" context={{ clientId: detail.id, currency: detail.currency }} />
            </div>
          )}
        </div>
      </div>

      {showAdd && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-card bg-surface p-6 shadow-xl">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-semibold">Add Client</h3>
              <button type="button" onClick={() => setShowAdd(false)}><X className="h-5 w-5 text-slate-400" /></button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                createClient.mutate();
              }}
              className="mt-4 space-y-3"
            >
              <input
                required
                placeholder="Company Name"
                value={form.companyName}
                onChange={(e) => setForm({ ...form, companyName: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <input
                type="email"
                placeholder="Email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <div className="grid grid-cols-2 gap-3">
                <input
                  placeholder="Phone"
                  value={form.phone}
                  onChange={(e) => setForm({ ...form, phone: e.target.value })}
                  className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
                />
                <input
                  placeholder="Currency (IDR/USD)"
                  value={form.currency}
                  onChange={(e) => setForm({ ...form, currency: e.target.value })}
                  className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
                />
              </div>
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setShowAdd(false)} className="rounded-control border border-slate-200 px-4 py-2 text-sm">Cancel</button>
                <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Save</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
