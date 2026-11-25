import React from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Briefcase, CheckSquare, FolderKanban } from 'lucide-react';
import { useAuth } from '../../app/AuthContext';
import { ExtensionSlot } from '../../extensions/ExtensionSlot';
import { DeadlineBadge } from '../../components/ui/DeadlineBadge';
import { apiFetch } from '../../lib/api';

interface DashboardData {
  kpis: { activeProjects: number; openTasks: number; overdueTasks: number; clientsCount: number; teamCount: number };
  projectProgress: { id: string; title: string; deadline?: string; status: string; progress: number }[];
  myTasks: { id: string; projectId: string; title: string; priority: string; status: string; deadline?: string }[];
  statusBreakdown: { toDo: number; inProgress: number; review: number; done: number; total: number };
  recentActivity: { id: string; action: string; entityName: string; entityId: string; createdAt: string }[];
}

export function DashboardPage() {
  const { user, tenant } = useAuth();
  const qc = useQueryClient();

  const { data } = useQuery({
    queryKey: ['dashboard', tenant?.id],
    queryFn: () => apiFetch<DashboardData>('/api/v1/workspace/dashboard'),
  });

  const markDone = useMutation({
    mutationFn: (taskId: string) =>
      apiFetch(`/api/v1/tasks/${taskId}/status`, { method: 'PATCH', body: JSON.stringify({ status: 'Done' }) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['dashboard'] }),
  });

  const kpis = data?.kpis ?? { activeProjects: 0, openTasks: 0, overdueTasks: 0, clientsCount: 0, teamCount: 1 };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Good morning, {user?.fullName}</h1>
        <p className="text-sm text-slate-500">
          Workspace: <span className="font-medium text-slate-700">{tenant?.companyName}</span>
        </p>
      </div>

      {/* 4 KPI Cards matching 01-tenant-dashboard.jpg */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Active Projects" value={String(kpis.activeProjects)} icon={<FolderKanban className="h-5 w-5 text-primary" />} />
        <StatCard label="Open Tasks" value={String(kpis.openTasks)} icon={<CheckSquare className="h-5 w-5 text-blue-600" />} />
        <StatCard label="Overdue Tasks" value={String(kpis.overdueTasks)} danger={kpis.overdueTasks > 0} icon={<AlertTriangle className="h-5 w-5 text-red-600" />} />
        <StatCard label="Clients" value={String(kpis.clientsCount)} icon={<Briefcase className="h-5 w-5 text-indigo-600" />} />
      </div>

      {/* Plugin Dashboard Widgets Extension Slot */}
      <ExtensionSlot
        name="dashboard.widgets"
        context={{ tenantId: tenant?.id }}
        className="grid grid-cols-1 gap-4 lg:grid-cols-2"
      />

      {/* Project Progress + My Tasks */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-semibold text-slate-900">Project Progress</h2>
            <Link to="/projects" className="text-xs font-medium text-primary hover:underline">View all</Link>
          </div>
          {!data?.projectProgress.length ? (
            <p className="text-sm text-slate-400">Belum ada proyek aktif.</p>
          ) : (
            <div className="space-y-3">
              {data.projectProgress.map((p) => (
                <div key={p.id} className="flex items-center gap-3 text-sm">
                  <Link to={`/projects/${p.id}`} className="w-36 truncate font-medium text-slate-800 hover:text-primary">
                    {p.title}
                  </Link>
                  <div className="h-2 flex-1 rounded-full bg-slate-100 overflow-hidden">
                    <div className="h-full bg-primary rounded-full" style={{ width: `${p.progress}%` }} />
                  </div>
                  <span className="w-10 text-right text-xs font-semibold text-slate-700">{p.progress}%</span>
                  <DeadlineBadge deadline={p.deadline} status={p.status} />
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-semibold text-slate-900">My Tasks</h2>
            <Link to="/tasks" className="text-xs font-medium text-primary hover:underline">View all</Link>
          </div>
          {!data?.myTasks.length ? (
            <p className="text-sm text-slate-400">Tidak ada tugas terbuka.</p>
          ) : (
            <div className="space-y-2.5">
              {data.myTasks.map((t) => (
                <div key={t.id} className="flex items-center justify-between text-sm">
                  <label className="flex items-center gap-2.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={false}
                      onChange={() => markDone.mutate(t.id)}
                      className="h-4 w-4 accent-primary"
                    />
                    <span className="text-slate-800">{t.title}</span>
                  </label>
                  <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">{t.priority}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Recent Activity + Tasks by Status */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2 rounded-card border border-slate-200 bg-surface p-5 space-y-3">
          <h2 className="text-base font-semibold text-slate-900">Recent Activity</h2>
          {!data?.recentActivity.length ? (
            <p className="text-sm text-slate-400">Belum ada aktivitas.</p>
          ) : (
            <div className="space-y-2.5">
              {data.recentActivity.map((a) => (
                <div key={a.id} className="flex items-center justify-between text-xs text-slate-600 border-b border-slate-100 pb-2 last:border-0">
                  <span>
                    <strong className="text-slate-800">{a.action}</strong> on <span className="font-medium">{a.entityName}</span>
                  </span>
                  <span className="text-slate-400">{new Date(a.createdAt).toLocaleTimeString()}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-3">
          <h2 className="text-base font-semibold text-slate-900">Tasks by Status ({data?.statusBreakdown.total ?? 0})</h2>
          <div className="space-y-2 text-sm">
            <StatusRow label="To Do" count={data?.statusBreakdown.toDo ?? 0} color="bg-indigo-600" />
            <StatusRow label="In Progress" count={data?.statusBreakdown.inProgress ?? 0} color="bg-blue-500" />
            <StatusRow label="Review" count={data?.statusBreakdown.review ?? 0} color="bg-amber-500" />
            <StatusRow label="Done" count={data?.statusBreakdown.done ?? 0} color="bg-emerald-600" />
          </div>
        </div>
      </div>
    </div>
  );
}

function StatCard({ label, value, icon, danger }: { label: string; value: string; icon: React.ReactNode; danger?: boolean }) {
  return (
    <div className="rounded-card border border-slate-200 bg-surface p-5">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-slate-500">{label}</span>
        <div className="rounded-lg bg-slate-50 p-2">{icon}</div>
      </div>
      <div className={`mt-2 text-3xl font-bold ${danger ? 'text-red-600' : 'text-slate-900'}`}>{value}</div>
    </div>
  );
}

function StatusRow({ label, count, color }: { label: string; count: number; color: string }) {
  return (
    <div className="flex items-center justify-between">
      <div className="flex items-center gap-2">
        <span className={`h-2.5 w-2.5 rounded-full ${color}`} />
        <span className="text-slate-700">{label}</span>
      </div>
      <span className="font-semibold text-slate-900">{count}</span>
    </div>
  );
}
