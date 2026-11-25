import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useAuth } from '../../app/AuthContext';
import { useTheme } from '../../theme/ThemeProvider';
import { ExtensionSlot } from '../../extensions/ExtensionSlot';
import { apiFetch } from '../../lib/api';

export function SettingsPage() {
  const { tenant } = useAuth();
  const { themeId, setThemeId, presets } = useTheme();
  const [companyName, setCompanyName] = useState(tenant?.companyName || '');
  const [saved, setSaved] = useState(false);

  const saveSettings = useMutation({
    mutationFn: () => apiFetch('/api/v1/tenant/settings', { method: 'PUT', body: JSON.stringify({ companyName }) }),
    onSuccess: () => setSaved(true),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Tenant Settings</h1>
        <p className="text-sm text-slate-500">Company profile, Free Core quota, and Theme Level 1.</p>
      </div>

      <div className="rounded-card border border-slate-200 bg-surface p-6 space-y-4 max-w-2xl">
        <h2 className="text-base font-semibold text-slate-900">Company Profile</h2>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setSaved(false);
            saveSettings.mutate();
          }}
          className="flex gap-3"
        >
          <input
            value={companyName}
            onChange={(e) => setCompanyName(e.target.value)}
            className="flex-1 rounded-control border border-slate-200 px-3 py-2 text-sm"
          />
          <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">
            Save
          </button>
        </form>
        {saved && <p className="text-xs text-emerald-600 font-medium">Settings saved.</p>}
      </div>

      <div className="rounded-card border border-slate-200 bg-surface p-6 space-y-4 max-w-2xl">
        <div>
          <h2 className="text-base font-semibold text-slate-900">Theme Level 1 (CSS Variables)</h2>
          <p className="text-xs text-slate-500">Ubah palet warna utama workspace secara instan.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          {presets.map((p) => (
            <button
              key={p.id}
              type="button"
              onClick={() => setThemeId(p.id)}
              className={`rounded-control border px-4 py-2 text-sm font-medium transition ${
                themeId === p.id
                  ? 'border-primary bg-primary text-white'
                  : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'
              }`}
            >
              {p.name}
            </button>
          ))}
        </div>
      </div>

      <ExtensionSlot name="settings.tenant.tabs" context={{ tenantId: tenant?.id }} />
    </div>
  );
}
