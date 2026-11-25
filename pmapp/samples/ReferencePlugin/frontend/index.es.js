import { jsx, jsxs } from 'react/jsx-runtime';

function RefDashboardWidget({ tenantId }) {
  return jsxs('div', {
    className: 'rounded-card border border-indigo-200 bg-indigo-50 p-4',
    children: [
      jsx('div', { className: 'text-sm font-semibold text-indigo-900', children: 'Reference Plugin Widget' }),
      jsx('div', { className: 'text-xs text-indigo-700 mt-1', children: `Loaded dynamically for tenant ${tenantId}` }),
    ],
  });
}

function RefPage() {
  return jsx('div', {
    className: 'rounded-card border border-slate-200 bg-surface p-6',
    children: jsx('h1', { className: 'text-xl font-bold text-slate-900', children: 'Reference Plugin Page' }),
  });
}

export function register(ctx) {
  ctx.registerAddOnMenuItem({
    id: 'ref-menu',
    pluginId: 'ref',
    label: 'Reference Add-on',
    path: '/p/ref',
    order: 10,
  });

  ctx.registerRoute({
    pluginId: 'ref',
    path: '/p/ref',
    component: RefPage,
  });

  ctx.registerSlotComponent({
    pluginId: 'ref',
    slotName: 'dashboard.widgets',
    id: 'ref-widget',
    component: RefDashboardWidget,
    order: 10,
  });
}
