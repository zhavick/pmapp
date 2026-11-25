import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, X } from 'lucide-react';
import { apiFetch } from '../../lib/api';
import { DeadlineBadge } from '../../components/ui/DeadlineBadge';
import { useUndoToast } from '../../components/ui/UndoToast';

interface ProjectListItem {
  id: string;
  title: string;
  clientName?: string;
  deadline?: string;
  status: string;
  progress: number;
  totalTasks: number;
  doneTasks: number;
}

export function ProjectsPage() {
  const qc = useQueryClient();
  const showUndo = useUndoToast();
  const [statusFilter, setStatusFilter] = useState('');
  const [showAdd, setShowAdd] = useState(false);
  const [form, setForm] = useState({ title: '', description: '', clientId: '', deadline: '' });

  const { data: projects = [] } = useQuery({
    queryKey: ['projects', statusFilter],
    queryFn: () => apiFetch<ProjectListItem[]>(`/api/v1/projects?status=${encodeURIComponent(statusFilter)}`),
  });

  const { data: clients = [] } = useQuery({
    queryKey: ['clients-mini'],
    queryFn: () => apiFetch<{ id: string; companyName: string }[]>('/api/v1/clients'),
  });

  const createProject = useMutation({
    mutationFn: () =>
      apiFetch('/api/v1/projects', {
        method: 'POST',
        body: JSON.stringify({
          title: form.title,
          description: form.description,
          clientId: form.clientId || null,
          deadline: form.deadline || null,
        }),
      }),
    onSuccess: () => {
      setShowAdd(false);
      setForm({ title: '', description: '', clientId: '', deadline: '' });
      qc.invalidateQueries({ queryKey: ['projects'] });
    },
  });

  const deleteProject = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/v1/projects/${id}`, { method: 'DELETE' }),
    onSuccess: (_, id) => {
      qc.invalidateQueries({ queryKey: ['projects'] });
      showUndo('Project deleted', async () => {
        await apiFetch(`/api/v1/projects/${id}/restore`, { method: 'POST' });
        qc.invalidateQueries({ queryKey: ['projects'] });
      });
    },
  });

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Projects</h1>
          <p className="text-sm text-slate-500">Track progress calculated automatically from Task Points.</p>
        </div>
        <div className="flex items-center gap-3">
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="rounded-control border border-slate-200 bg-surface px-3 py-2 text-sm"
          >
            <option value="">All Statuses</option>
            <option value="Open">Open</option>
            <option value="InProgress">In Progress</option>
            <option value="Completed">Completed</option>
            <option value="OnHold">On Hold</option>
          </select>
          <button
            type="button"
            onClick={() => setShowAdd(true)}
            className="inline-flex items-center gap-2 rounded-control bg-primary px-4 py-2 text-sm font-medium text-white hover:opacity-95"
          >
            <Plus className="h-4 w-4" />
            Add Project
          </button>
        </div>
      </div>

      <div className="rounded-card border border-slate-200 bg-surface overflow-hidden">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Project</th>
              <th className="px-4 py-3">Client</th>
              <th className="px-4 py-3">Deadline</th>
              <th className="px-4 py-3">Progress</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {projects.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-slate-400">
                  Belum ada proyek. Klik "Add Project" untuk memulai.
                </td>
              </tr>
            ) : (
              projects.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-medium">
                    <Link to={`/projects/${p.id}`} className="text-primary hover:underline">
                      {p.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{p.clientName || '-'}</td>
                  <td className="px-4 py-3"><DeadlineBadge deadline={p.deadline} status={p.status} /></td>
                  <td className="px-4 py-3 w-48">
                    <div className="flex items-center gap-2">
                      <div className="h-2 flex-1 rounded-full bg-slate-100 overflow-hidden">
                        <div className="h-full bg-primary rounded-full" style={{ width: `${p.progress}%` }} />
                      </div>
                      <span className="text-xs font-medium text-slate-600">{p.progress}%</span>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span className="rounded bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700">{p.status}</span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      type="button"
                      onClick={() => deleteProject.mutate(p.id)}
                      className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600"
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

      {showAdd && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-card bg-surface p-6 shadow-xl">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-semibold">Add Project</h3>
              <button type="button" onClick={() => setShowAdd(false)}><X className="h-5 w-5 text-slate-400" /></button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                createProject.mutate();
              }}
              className="mt-4 space-y-3"
            >
              <input
                required
                placeholder="Project Title"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <select
                value={form.clientId}
                onChange={(e) => setForm({ ...form, clientId: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              >
                <option value="">No Client (Internal)</option>
                {clients.map((c) => (
                  <option key={c.id} value={c.id}>{c.companyName}</option>
                ))}
              </select>
              <input
                type="date"
                value={form.deadline}
                onChange={(e) => setForm({ ...form, deadline: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <textarea
                rows={3}
                placeholder="Description"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setShowAdd(false)} className="rounded-control border border-slate-200 px-4 py-2 text-sm">Cancel</button>
                <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Create Project</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
