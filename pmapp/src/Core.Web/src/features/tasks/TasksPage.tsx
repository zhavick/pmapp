import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '../../lib/api';
import { DeadlineBadge } from '../../components/ui/DeadlineBadge';

interface GlobalTaskItem {
  id: string;
  projectId: string;
  projectTitle: string;
  title: string;
  points: number;
  status: string;
  priority: string;
  deadline?: string;
}

export function TasksPage() {
  const qc = useQueryClient();
  const { data: tasks = [] } = useQuery({
    queryKey: ['global-tasks'],
    queryFn: () => apiFetch<GlobalTaskItem[]>('/api/v1/tasks'),
  });

  const toggleDone = useMutation({
    mutationFn: (t: GlobalTaskItem) =>
      apiFetch(`/api/v1/tasks/${t.id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status: t.status === 'Done' ? 'ToDo' : 'Done' }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['global-tasks'] }),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Tasks</h1>
        <p className="text-sm text-slate-500">All tasks across active projects.</p>
      </div>

      <div className="rounded-card border border-slate-200 bg-surface overflow-hidden">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 w-10">Done</th>
              <th className="px-4 py-3">Task</th>
              <th className="px-4 py-3">Project</th>
              <th className="px-4 py-3">Priority</th>
              <th className="px-4 py-3">Points</th>
              <th className="px-4 py-3">Deadline</th>
              <th className="px-4 py-3">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {tasks.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-400">Belum ada tugas.</td>
              </tr>
            ) : (
              tasks.map((t) => (
                <tr key={t.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      checked={t.status === 'Done'}
                      onChange={() => toggleDone.mutate(t)}
                      className="h-4 w-4 accent-primary"
                    />
                  </td>
                  <td className={`px-4 py-3 font-medium ${t.status === 'Done' ? 'line-through text-slate-400' : 'text-slate-900'}`}>
                    {t.title}
                  </td>
                  <td className="px-4 py-3">
                    <Link to={`/projects/${t.projectId}`} className="text-primary hover:underline">{t.projectTitle}</Link>
                  </td>
                  <td className="px-4 py-3 text-xs font-medium">{t.priority}</td>
                  <td className="px-4 py-3 text-xs">{t.points} pts</td>
                  <td className="px-4 py-3"><DeadlineBadge deadline={t.deadline} status={t.status} /></td>
                  <td className="px-4 py-3">
                    <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">{t.status}</span>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
