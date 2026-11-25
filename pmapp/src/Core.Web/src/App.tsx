import React from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider, useAuth } from './app/AuthContext';
import { ExtensionProvider, useExtensions } from './extensions/ExtensionRegistry';
import { ThemeProvider } from './theme/ThemeProvider';
import { UndoToastProvider } from './components/ui/UndoToast';
import { TenantAppShell } from './layouts/TenantAppShell';
import { PlatformShell } from './layouts/PlatformShell';
import { LoginPage, RegisterTenantPage } from './features/auth/AuthPages';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { ClientsPage } from './features/clients/ClientsPage';
import { TeamPage } from './features/team/TeamPage';
import { ProjectsPage } from './features/projects/ProjectsPage';
import { ProjectDetailPage } from './features/projects/ProjectDetailPage';
import { TasksPage } from './features/tasks/TasksPage';
import { NotesTodoPage } from './features/notes/NotesTodoPage';
import { SettingsPage } from './features/settings/SettingsPage';
import { TenantPluginsPage } from './features/plugins/TenantPluginsPage';
import {
  PlatformAuditPage,
  PlatformInvoicesPage,
  PlatformLicenseRequestsPage,
  PlatformLicensesPage,
  PlatformPluginsPage,
  PlatformTenantsPage,
} from './features/platform/PlatformAdminPages';

const queryClient = new QueryClient();

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) {
    return <div className="flex min-h-screen items-center justify-center text-sm text-slate-500">Loading workspace...</div>;
  }
  return user ? <>{children}</> : <Navigate to="/login" replace />;
}

function AppRoutes() {
  const { routes: pluginRoutes } = useExtensions();

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterTenantPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <TenantAppShell />
          </ProtectedRoute>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="projects" element={<ProjectsPage />} />
        <Route path="projects/:id" element={<ProjectDetailPage />} />
        <Route path="tasks" element={<TasksPage />} />
        <Route path="clients" element={<ClientsPage />} />
        <Route path="team" element={<TeamPage />} />
        <Route path="notes" element={<NotesTodoPage />} />
        <Route path="plugins" element={<TenantPluginsPage />} />
        <Route path="settings" element={<SettingsPage />} />
        {pluginRoutes.map((r) => {
          const Comp = r.component;
          return <Route key={`${r.pluginId}:${r.path}`} path={r.path.replace(/^\//, '')} element={<Comp />} />;
        })}
      </Route>
      <Route
        path="/platform"
        element={
          <ProtectedRoute>
            <PlatformShell />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="license-requests" replace />} />
        <Route path="tenants" element={<PlatformTenantsPage />} />
        <Route path="plugins" element={<PlatformPluginsPage />} />
        <Route path="licenses" element={<PlatformLicensesPage />} />
        <Route path="license-requests" element={<PlatformLicenseRequestsPage />} />
        <Route path="invoices" element={<PlatformInvoicesPage />} />
        <Route path="audit" element={<PlatformAuditPage />} />
      </Route>
    </Routes>
  );
}

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <UndoToastProvider>
          <AuthProvider>
            <ExtensionProvider>
              <BrowserRouter>
                <AppRoutes />
              </BrowserRouter>
            </ExtensionProvider>
          </AuthProvider>
        </UndoToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
