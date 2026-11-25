import React, { useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import {
  Activity,
  Building2,
  FileText,
  KeyRound,
  LayoutGrid,
  LogOut,
  Plug,
  Receipt,
  ShieldCheck,
  Sparkles,
  X,
} from 'lucide-react';
import { useAuth } from '../app/AuthContext';

const adminNav = [
  { to: '/platform/tenants', label: 'Tenants', icon: Building2 },
  { to: '/platform/plugins', label: 'Plugin Catalog', icon: Plug },
  { to: '/platform/licenses', label: 'License Numbers', icon: KeyRound },
  { to: '/platform/license-requests', label: 'License Requests', icon: ShieldCheck },
  { to: '/platform/invoices', label: 'Invoices', icon: Receipt },
  { to: '/platform/audit', label: 'Audit Log', icon: FileText },
];

export function PlatformShell() {
  const { user, logout } = useAuth();
  const [aiModalOpen, setAiModalOpen] = useState(false);

  return (
    <div className="flex min-h-screen bg-[#111827] text-slate-100">
      {/* Dark Charcoal Sidebar */}
      <aside className="flex w-[250px] shrink-0 flex-col border-r border-slate-800 bg-[#0B101B]">
        <div className="flex items-center gap-2.5 px-5 pt-5 pb-4">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-teal-500 text-slate-950 font-bold">
            P
          </div>
          <span className="text-base font-bold text-white">PM App</span>
          <span className="rounded bg-red-600 px-1.5 py-0.5 text-[10px] font-bold uppercase text-white">
            PLATFORM ADMIN
          </span>
        </div>

        <nav className="flex-1 space-y-1 px-3 pt-2">
          {adminNav.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition ${
                    isActive
                      ? 'bg-teal-500/20 text-teal-300 border border-teal-500/40'
                      : 'text-slate-400 hover:bg-slate-800/70 hover:text-slate-100'
                  }`
                }
              >
                <Icon className="h-4 w-4 shrink-0" />
                <span>{item.label}</span>
              </NavLink>
            );
          })}

          <div className="pt-4 mt-4 border-t border-slate-800">
            <Link
              to="/"
              className="flex items-center gap-3 rounded-lg px-3 py-2 text-xs font-medium text-slate-400 hover:bg-slate-800 hover:text-white"
            >
              <LayoutGrid className="h-4 w-4" />
              Back to Tenant App
            </Link>
          </div>
        </nav>

        <div className="border-t border-slate-800 p-3">
          <button
            type="button"
            onClick={() => setAiModalOpen(true)}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-teal-300 hover:bg-slate-800/80"
          >
            <Sparkles className="h-4 w-4 shrink-0 text-teal-400" />
            <span>AI Assistant</span>
          </button>
        </div>
      </aside>

      <div className="flex flex-1 flex-col min-w-0">
        <header className="flex h-16 items-center justify-between border-b border-slate-800 bg-[#0F1624] px-6">
          <div className="flex items-center gap-2 text-xs text-teal-400">
            <Activity className="h-4 w-4" />
            <span>Platform Administration Console</span>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium text-slate-200">{user?.fullName} (Super Admin)</span>
            <button type="button" onClick={logout} className="rounded p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white">
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        </header>

        <main className="flex-1 p-6 overflow-y-auto">
          <Outlet />
        </main>
      </div>

      {aiModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div className="w-full max-w-md rounded-xl border border-slate-700 bg-slate-900 p-6 text-slate-100">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <Sparkles className="h-5 w-5 text-teal-400" />
                <h3 className="font-semibold">AI Assistant</h3>
              </div>
              <button type="button" onClick={() => setAiModalOpen(false)}><X className="h-5 w-5 text-slate-400" /></button>
            </div>
            <p className="mt-4 text-sm text-slate-300">
              Menu pengingat masa depan untuk asisten AI Platform Admin (Fase 7+).
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
