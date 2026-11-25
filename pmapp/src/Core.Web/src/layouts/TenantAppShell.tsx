import React, { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import {
  Bell,
  CheckSquare,
  ChevronDown,
  FolderKanban,
  LayoutDashboard,
  LogOut,
  Mail,
  NotebookPen,
  Plug,
  Plus,
  Search,
  Settings,
  Sparkles,
  Users,
  Briefcase,
  X,
} from 'lucide-react';
import { useAuth } from '../app/AuthContext';
import { useExtensions } from '../extensions/ExtensionRegistry';
import { ExtensionSlot } from '../extensions/ExtensionSlot';

const coreNav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/projects', label: 'Projects', icon: FolderKanban },
  { to: '/tasks', label: 'Tasks', icon: CheckSquare },
  { to: '/clients', label: 'Clients', icon: Briefcase },
  { to: '/team', label: 'Team', icon: Users },
  { to: '/notes', label: 'Notes & To-Do', icon: NotebookPen },
  { to: '/plugins', label: 'Plugins', icon: Plug },
  { to: '/settings', label: 'Settings', icon: Settings },
];

export function TenantAppShell() {
  const { user, tenant, workspaces, activePlugins, switchTenant, logout } = useAuth();
  const { addOnMenuItems } = useExtensions();
  const [aiModalOpen, setAiModalOpen] = useState(false);

  const initials = (tenant?.companyName || 'PM')
    .split(' ')
    .map((w) => w[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  return (
    <div className="flex min-h-screen bg-canvas text-slate-900">
      {/* Left Sidebar (Dark Navy #0F172A) */}
      <aside className="flex w-[260px] shrink-0 flex-col bg-sidebar text-slate-200">
        {/* Brand */}
        <div className="flex items-center gap-3 px-5 pt-5 pb-4">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-white font-bold">
            P
          </div>
          <span className="text-lg font-bold tracking-tight text-white">PM App</span>
        </div>

        {/* Workspace Switcher */}
        <div className="px-4 pb-4">
          <div className="relative flex items-center justify-between rounded-lg border border-slate-700/70 bg-slate-800/60 px-3 py-2">
            <div className="flex items-center gap-2.5 overflow-hidden">
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-slate-700 text-xs font-semibold text-white">
                {initials}
              </div>
              <select
                aria-label="Workspace Switcher"
                value={tenant?.id || ''}
                onChange={(e) => switchTenant(e.target.value)}
                className="w-full cursor-pointer truncate bg-transparent text-sm font-medium text-white focus:outline-none"
              >
                {workspaces.map((w) => (
                  <option key={w.id} value={w.id} className="bg-slate-900 text-white">
                    {w.companyName}
                  </option>
                ))}
              </select>
            </div>
            <ChevronDown className="h-4 w-4 shrink-0 text-slate-400 pointer-events-none" />
          </div>
        </div>

        {/* Core Navigation */}
        <nav className="flex-1 space-y-1 px-3 overflow-y-auto">
          {coreNav.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-control px-3 py-2.5 text-sm font-medium transition ${
                    isActive
                      ? 'bg-primary text-white shadow-sm'
                      : 'text-slate-300 hover:bg-slate-800/70 hover:text-white'
                  }`
                }
              >
                <Icon className="h-4 w-4 shrink-0" />
                <span>{item.label}</span>
              </NavLink>
            );
          })}

          {/* Dynamic ADD-ONS section (only visible when at least 1 plugin is active) */}
          {(activePlugins.length > 0 || addOnMenuItems.length > 0) && (
            <div className="pt-4 mt-4 border-t border-slate-800">
              <div className="px-3 pb-2 text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                Add-ons
              </div>
              {addOnMenuItems.length > 0
                ? addOnMenuItems.map((item) => (
                    <NavLink
                      key={`${item.pluginId}:${item.id}`}
                      to={item.path}
                      className={({ isActive }) =>
                        `flex items-center gap-3 rounded-control px-3 py-2 text-sm font-medium transition ${
                          isActive
                            ? 'bg-primary text-white shadow-sm'
                            : 'text-slate-300 hover:bg-slate-800/70 hover:text-white'
                        }`
                      }
                    >
                      <Plug className="h-4 w-4 text-indigo-400" />
                      <span>{item.label}</span>
                    </NavLink>
                  ))
                : activePlugins.map((pid) => (
                    <div
                      key={pid}
                      className="flex items-center gap-3 rounded-control px-3 py-2 text-sm text-slate-300"
                    >
                      <Plug className="h-4 w-4 text-indigo-400" />
                      <span className="capitalize">{pid}</span>
                    </div>
                  ))}
              <ExtensionSlot name="sidebar.addons" context={{ tenantId: tenant?.id }} />
            </div>
          )}
        </nav>

        {/* Fixed Footer Reminder: AI Assistant */}
        <div className="border-t border-slate-800 p-3">
          <button
            type="button"
            onClick={() => setAiModalOpen(true)}
            className="flex w-full items-center gap-3 rounded-control px-3 py-2.5 text-sm font-medium text-indigo-300 hover:bg-slate-800/80 hover:text-white transition"
          >
            <Sparkles className="h-4 w-4 shrink-0 text-indigo-400" />
            <span>AI Assistant</span>
          </button>
        </div>
      </aside>

      {/* Main Content Wrapper */}
      <div className="flex flex-1 flex-col min-w-0">
        {/* Topbar (64px) */}
        <header className="flex h-16 shrink-0 items-center justify-between border-b border-slate-200 bg-surface px-6">
          <div className="relative w-80">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Search projects, tasks, clients..."
              className="w-full rounded-control border border-slate-200 bg-slate-50 py-1.5 pl-9 pr-3 text-sm focus:border-primary focus:bg-white focus:outline-none"
            />
          </div>

          <div className="flex items-center gap-4">
            <button
              type="button"
              className="inline-flex items-center gap-1.5 rounded-control bg-primary px-3.5 py-2 text-sm font-medium text-white shadow-sm hover:opacity-95"
            >
              <Plus className="h-4 w-4" />
              Quick Add
            </button>
            <button type="button" className="relative rounded-full p-2 text-slate-500 hover:bg-slate-100" title="Notifications">
              <Bell className="h-5 w-5" />
            </button>
            <button type="button" className="rounded-full p-2 text-slate-500 hover:bg-slate-100" title="Messages">
              <Mail className="h-5 w-5" />
            </button>
            <div className="flex items-center gap-2.5 border-l border-slate-200 pl-4">
              <div className="flex h-8 w-8 items-center justify-center rounded-full bg-indigo-100 text-xs font-bold text-primary">
                {user?.fullName?.[0]?.toUpperCase() || 'U'}
              </div>
              <div className="text-sm">
                <div className="font-medium text-slate-800">{user?.fullName}</div>
              </div>
              <button
                type="button"
                onClick={logout}
                title="Sign out"
                className="ml-1 rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </div>
          </div>
        </header>

        <main className="flex-1 p-6 overflow-y-auto">
          <Outlet />
        </main>
      </div>

      {/* AI Assistant Future Reminder Modal */}
      {aiModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-card bg-surface p-6 shadow-xl border border-slate-200">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <Sparkles className="h-5 w-5 text-primary" />
                <h3 className="text-base font-semibold text-slate-900">AI Assistant</h3>
              </div>
              <button type="button" onClick={() => setAiModalOpen(false)} className="text-slate-400 hover:text-slate-700">
                <X className="h-5 w-5" />
              </button>
            </div>
            <p className="mt-4 text-sm text-slate-600 leading-relaxed">
              Menu <strong>AI Assistant</strong> ini disiapkan sebagai pengingat pengembangan fitur masa depan
              (<em>AI Project &amp; CRM Copilot Plugin</em> pada Fase 7+).
            </p>
            <div className="mt-5 flex justify-end">
              <button
                type="button"
                onClick={() => setAiModalOpen(false)}
                className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white"
              >
                Tutup
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
