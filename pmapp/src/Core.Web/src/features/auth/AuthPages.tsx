import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate('/');
    } catch (err: any) {
      setError(err.message || 'Login failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas px-4">
      <div className="w-full max-w-md rounded-card border border-slate-200 bg-surface p-8 shadow-sm">
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-white font-bold">
            P
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">Sign in to PM App</h1>
            <p className="text-xs text-slate-500">Multi-Tenant Project Management &amp; CRM</p>
          </div>
        </div>

        {error && (
          <div className="mb-4 rounded-control border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Email</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-control border border-slate-300 px-3 py-2 text-sm focus:border-primary focus:outline-none"
              placeholder="rina@acmestudio.com"
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Password</label>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-control border border-slate-300 px-3 py-2 text-sm focus:border-primary focus:outline-none"
            />
          </div>
          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-control bg-primary py-2.5 text-sm font-semibold text-white hover:opacity-95 disabled:opacity-60"
          >
            {submitting ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <p className="mt-5 text-center text-sm text-slate-600">
          Belum punya workspace?{' '}
          <Link to="/register" className="font-medium text-primary hover:underline">
            Daftar Perusahaan Gratis
          </Link>
        </p>
      </div>
    </div>
  );
}

export function RegisterTenantPage() {
  const { registerTenant } = useAuth();
  const navigate = useNavigate();
  const [companyName, setCompanyName] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await registerTenant(companyName, fullName, email, password);
      navigate('/');
    } catch (err: any) {
      setError(err.message || 'Registration failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas px-4">
      <div className="w-full max-w-md rounded-card border border-slate-200 bg-surface p-8 shadow-sm">
        <div className="mb-6">
          <h1 className="text-xl font-bold text-slate-900">Buat Workspace Perusahaan</h1>
          <p className="text-xs text-slate-500">Gratis untuk modul inti (maks 10 pengguna, 2 GB penyimpanan)</p>
        </div>

        {error && (
          <div className="mb-4 rounded-control border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Nama Perusahaan</label>
            <input
              type="text"
              required
              value={companyName}
              onChange={(e) => setCompanyName(e.target.value)}
              className="w-full rounded-control border border-slate-300 px-3 py-2 text-sm focus:border-primary focus:outline-none"
              placeholder="Acme Studio"
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Nama Lengkap Admin</label>
            <input
              type="text"
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              className="w-full rounded-control border border-slate-300 px-3 py-2 text-sm focus:border-primary focus:outline-none"
              placeholder="Rina"
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Email Kerja</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-control border border-slate-300 px-3 py-2 text-sm focus:border-primary focus:outline-none"
              placeholder="rina@acmestudio.com"
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700">Password (min 6 karakter)</label>
            <input
              type="password"
              required
              minLength={6}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-control border border-slate-300 px-3 py-2 text-sm focus:border-primary focus:outline-none"
            />
          </div>
          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-control bg-primary py-2.5 text-sm font-semibold text-white hover:opacity-95 disabled:opacity-60"
          >
            {submitting ? 'Membuat Workspace...' : 'Buat Workspace'}
          </button>
        </form>

        <p className="mt-5 text-center text-sm text-slate-600">
          Sudah punya akun?{' '}
          <Link to="/login" className="font-medium text-primary hover:underline">
            Sign In
          </Link>
        </p>
      </div>
    </div>
  );
}
