import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Shield, UserPlus, X } from 'lucide-react';
import { apiFetch } from '../../lib/api';

interface TeamResponse {
  maxUsersQuota: number;
  currentUsers: number;
  members: {
    membershipId: string;
    userId: string;
    fullName: string;
    email: string;
    roleId: string;
    roleName: string;
    isOwner: boolean;
  }[];
}

interface RoleItem {
  id: string;
  name: string;
  isSystem: boolean;
}

interface PermItem {
  key: string;
  displayName: string;
  category: string;
  pluginId: string | null;
}

export function TeamPage() {
  const qc = useQueryClient();
  const [showAdd, setShowAdd] = useState(false);
  const [selectedRoleId, setSelectedRoleId] = useState<string>('');
  const [form, setForm] = useState({ fullName: '', email: '', password: '', roleId: '' });
  const [error, setError] = useState<string | null>(null);

  const { data: team } = useQuery({
    queryKey: ['team'],
    queryFn: () => apiFetch<TeamResponse>('/api/v1/team'),
  });

  const { data: roles = [] } = useQuery({
    queryKey: ['rbac-roles'],
    queryFn: () => apiFetch<RoleItem[]>('/api/v1/rbac/roles'),
  });

  const { data: allPermissions = [] } = useQuery({
    queryKey: ['rbac-permissions'],
    queryFn: () => apiFetch<PermItem[]>('/api/v1/rbac/permissions'),
  });

  const { data: rolePerms = [] } = useQuery({
    queryKey: ['role-perms', selectedRoleId],
    queryFn: () => apiFetch<{ permissionKey: string; pluginId: string | null }[]>(`/api/v1/rbac/roles/${selectedRoleId}/permissions`),
    enabled: !!selectedRoleId,
  });

  const addMember = useMutation({
    mutationFn: () =>
      apiFetch('/api/v1/team/members', {
        method: 'POST',
        body: JSON.stringify({ ...form, roleId: form.roleId || roles[0]?.id }),
      }),
    onSuccess: () => {
      setShowAdd(false);
      setError(null);
      setForm({ fullName: '', email: '', password: '', roleId: '' });
      qc.invalidateQueries({ queryKey: ['team'] });
    },
    onError: (err: any) => setError(err.message),
  });

  const togglePermission = useMutation({
    mutationFn: (perm: PermItem) => {
      const exists = rolePerms.some((rp) => rp.permissionKey === perm.key);
      const next = exists
        ? rolePerms.filter((rp) => rp.permissionKey !== perm.key).map((rp) => ({ key: rp.permissionKey, pluginId: rp.pluginId }))
        : [...rolePerms.map((rp) => ({ key: rp.permissionKey, pluginId: rp.pluginId })), { key: perm.key, pluginId: perm.pluginId }];

      return apiFetch(`/api/v1/rbac/roles/${selectedRoleId}/permissions`, {
        method: 'PUT',
        body: JSON.stringify({ permissions: next }),
      });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['role-perms', selectedRoleId] }),
  });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Team &amp; RBAC</h1>
          <p className="text-sm text-slate-500">
            Quota usage: <strong>{team?.currentUsers ?? 0} / {team?.maxUsersQuota ?? 10}</strong> members (Free Core Plan)
          </p>
        </div>
        <button
          type="button"
          onClick={() => setShowAdd(true)}
          className="inline-flex items-center gap-2 rounded-control bg-primary px-4 py-2 text-sm font-medium text-white hover:opacity-95"
        >
          <UserPlus className="h-4 w-4" />
          Add Team Member
        </button>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Members Table */}
        <div className="rounded-card border border-slate-200 bg-surface overflow-hidden">
          <div className="border-b border-slate-200 bg-slate-50 px-4 py-3 text-sm font-semibold text-slate-800">
            Team Members
          </div>
          <table className="w-full text-left text-sm">
            <tbody className="divide-y divide-slate-100">
              {team?.members.map((m) => (
                <tr key={m.membershipId}>
                  <td className="px-4 py-3">
                    <div className="font-medium text-slate-900">{m.fullName}</div>
                    <div className="text-xs text-slate-500">{m.email}</div>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="rounded bg-indigo-50 px-2.5 py-1 text-xs font-medium text-primary">
                      {m.roleName}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Dynamic RBAC Permissions */}
        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Shield className="h-5 w-5 text-primary" />
              <h2 className="text-base font-semibold text-slate-900">Role Permissions</h2>
            </div>
            <select
              value={selectedRoleId}
              onChange={(e) => setSelectedRoleId(e.target.value)}
              className="rounded-control border border-slate-200 px-3 py-1.5 text-sm"
            >
              <option value="">Select Role...</option>
              {roles.map((r) => (
                <option key={r.id} value={r.id}>{r.name}</option>
              ))}
            </select>
          </div>

          <p className="text-xs text-slate-500">
            Permissions dari plugin hanya muncul di sini jika plugin tersebut aktif pada perusahaan Anda.
          </p>

          {selectedRoleId && (
            <div className="space-y-2 max-h-72 overflow-y-auto">
              {allPermissions.map((p) => {
                const checked = rolePerms.some((rp) => rp.permissionKey === p.key);
                return (
                  <label key={p.key} className="flex items-center justify-between rounded-control border border-slate-100 px-3 py-2 text-sm cursor-pointer hover:bg-slate-50">
                    <div>
                      <span className="font-medium text-slate-800">{p.displayName}</span>
                      <span className="ml-2 text-xs text-slate-400">({p.category})</span>
                    </div>
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => togglePermission.mutate(p)}
                      className="h-4 w-4 accent-primary"
                    />
                  </label>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {showAdd && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-card bg-surface p-6 shadow-xl">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-semibold">Add Team Member</h3>
              <button type="button" onClick={() => setShowAdd(false)}><X className="h-5 w-5 text-slate-400" /></button>
            </div>
            {error && <div className="mt-3 rounded bg-red-50 p-2 text-xs text-red-700">{error}</div>}
            <form
              onSubmit={(e) => {
                e.preventDefault();
                addMember.mutate();
              }}
              className="mt-4 space-y-3"
            >
              <input
                required
                placeholder="Full Name"
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <input
                type="email"
                required
                placeholder="Email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <input
                type="password"
                required
                minLength={6}
                placeholder="Initial Password"
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <select
                value={form.roleId || roles[0]?.id || ''}
                onChange={(e) => setForm({ ...form, roleId: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              >
                {roles.map((r) => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setShowAdd(false)} className="rounded-control border border-slate-200 px-4 py-2 text-sm">Cancel</button>
                <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Add Member</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
