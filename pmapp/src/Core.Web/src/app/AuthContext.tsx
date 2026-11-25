import React, { createContext, useContext, useEffect, useState } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../lib/api';

export interface UserInfo {
  id: string;
  email: string;
  fullName: string;
  isSuperAdmin: boolean;
}

export interface TenantInfo {
  id: string;
  slug: string;
  companyName: string;
  maxUsersQuota: number;
  maxStorageBytes: number;
  usedStorageBytes?: number;
}

export interface WorkspaceSummary {
  id: string;
  slug: string;
  companyName: string;
}

interface AuthContextValue {
  user: UserInfo | null;
  tenant: TenantInfo | null;
  workspaces: WorkspaceSummary[];
  activePlugins: string[];
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  registerTenant: (companyName: string, fullName: string, email: string, password: string) => Promise<void>;
  switchTenant: (targetTenantId: string) => Promise<void>;
  refreshAuth: () => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [workspaces, setWorkspaces] = useState<WorkspaceSummary[]>([]);
  const [activePlugins, setActivePlugins] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  const loadMe = async () => {
    if (!getAccessToken()) {
      setLoading(false);
      return;
    }
    try {
      const data = await apiFetch<{
        user: UserInfo;
        tenant: TenantInfo | null;
        workspaces: WorkspaceSummary[];
        activePlugins: string[];
      }>('/api/v1/auth/me');
      setUser(data.user);
      setTenant(data.tenant);
      setWorkspaces(data.workspaces);
      setActivePlugins(data.activePlugins);
    } catch {
      setAccessToken(null);
      setUser(null);
      setTenant(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMe();
  }, []);

  const login = async (email: string, password: string) => {
    const res = await apiFetch<{
      accessToken: string;
      user: UserInfo;
      tenant: TenantInfo;
      activePlugins: string[];
    }>('/api/v1/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    setAccessToken(res.accessToken);
    await loadMe();
  };

  const registerTenant = async (companyName: string, fullName: string, email: string, password: string) => {
    const res = await apiFetch<{ accessToken: string }>('/api/v1/auth/register-tenant', {
      method: 'POST',
      body: JSON.stringify({ companyName, fullName, email, password }),
    });
    setAccessToken(res.accessToken);
    await loadMe();
  };

  const switchTenant = async (targetTenantId: string) => {
    const res = await apiFetch<{ accessToken: string }>('/api/v1/auth/switch-tenant', {
      method: 'POST',
      body: JSON.stringify({ targetTenantId }),
    });
    setAccessToken(res.accessToken);
    await loadMe();
  };

  const logout = () => {
    setAccessToken(null);
    setUser(null);
    setTenant(null);
    setWorkspaces([]);
    setActivePlugins([]);
  };

  return (
    <AuthContext.Provider
      value={{ user, tenant, workspaces, activePlugins, loading, login, registerTenant, switchTenant, refreshAuth: loadMe, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
