import React, { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Layers, Plus, Trash2, X } from 'lucide-react';
import { apiFetch } from '../../lib/api';
import { DeadlineBadge } from '../../components/ui/DeadlineBadge';
import { ExtensionSlot } from '../../extensions/ExtensionSlot';
import { useUndoToast } from '../../components/ui/UndoToast';

interface TaskItem {
  id: string;
  projectId: string;
  milestoneId?: string;
  title: string;
  description: string;
  points: number;
  status: 'ToDo' | 'InProgress' | 'Review' | 'Done';
  priority: string;
  deadline?: string;
}

interface MilestoneItem {
  id: string;
  title: string;
  dueDate?: string;
  progress: number;
  totalTasks: number;
  doneTasks: number;
}

interface ProjectDetail {
  id: string;
  title: string;
  description: string;
  clientId?: string;
  clientName?: string;
  deadline?: string;
  status: string;
  progress: number;
  milestones: MilestoneItem[];
  tasks: TaskItem[];
}

const COLUMNS: { key: TaskItem['status']; label: string }[] = [
  { key: 'ToDo', label: 'To Do' },
  { key: 'InProgress', label: 'In Progress' },
  { key: 'Review', label: 'Review' },
  { key: 'Done', label: 'Done' },
];

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const showUndo = useUndoToast();
  const [tab, setTab] = useState<'overview' | 'tasks' | 'milestones'>('tasks');
  const [showAddTask, setShowAddTask] = useState(false);
  const [showAddMilestone, setShowAddMilestone] = useState(false);
  const [selectedTask, setSelectedTask] = useState<TaskItem | null>(null);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [batchStatus, setBatchStatus] = useState('Done');

  const [taskForm, setTaskForm] = useState({ title: '', points: 2, priority: 'Medium', milestoneId: '', deadline: '' });
  const [msForm, setMsForm] = useState({ title: '', dueDate: '' });

  const { data: project } = useQuery({
    queryKey: ['project-detail', id],
    queryFn: () => apiFetch<ProjectDetail>(`/api/v1/projects/${id}`),
    enabled: !!id,
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['project-detail', id] });
    qc.invalidateQueries({ queryKey: ['projects'] });
  };

  const createTask = useMutation({
    mutationFn: () =>
      apiFetch(`/api/v1/projects/${id}/tasks`, {
        method: 'POST',
        body: JSON.stringify({
          title: taskForm.title,
          points: Number(taskForm.points) || 1,
          priority: taskForm.priority,
          milestoneId: taskForm.milestoneId || null,
          deadline: taskForm.deadline || null,
        }),
      }),
    onSuccess: () => {
      setShowAddTask(false);
      setTaskForm({ title: '', points: 2, priority: 'Medium', milestoneId: '', deadline: '' });
      refresh();
    },
  });

  const updateTaskStatus = useMutation({
    mutationFn: ({ taskId, status }: { taskId: string; status: string }) =>
      apiFetch(`/api/v1/tasks/${taskId}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status }),
      }),
    onSuccess: refresh,
  });

  const batchUpdate = useMutation({
    mutationFn: () =>
      apiFetch('/api/v1/tasks/batch-update', {
        method: 'POST',
        body: JSON.stringify({ taskIds: selectedIds, status: batchStatus }),
      }),
    onSuccess: () => {
      setSelectedIds([]);
      refresh();
    },
  });

  const deleteTask = useMutation({
    mutationFn: (taskId: string) => apiFetch(`/api/v1/tasks/${taskId}`, { method: 'DELETE' }),
    onSuccess: (_, taskId) => {
      setSelectedTask(null);
      refresh();
      showUndo('Task deleted', async () => {
        await apiFetch(`/api/v1/tasks/${taskId}/restore`, { method: 'POST' });
        refresh();
      });
    },
  });

  const createMilestone = useMutation({
    mutationFn: () =>
      apiFetch(`/api/v1/projects/${id}/milestones`, {
        method: 'POST',
        body: JSON.stringify({ title: msForm.title, dueDate: msForm.dueDate || null, sortOrder: project?.milestones.length ?? 0 }),
      }),
    onSuccess: () => {
      setShowAddMilestone(false);
      setMsForm({ title: '', dueDate: '' });
      refresh();
    },
  });

  if (!project) return <div className="text-sm text-slate-500">Loading project...</div>;

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="rounded-card border border-slate-200 bg-surface p-5">
        <div className="text-xs text-slate-500 mb-1">
          <Link to="/projects" className="hover:underline">Projects</Link> / {project.title}
        </div>
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{project.title}</h1>
            <div className="mt-2 flex flex-wrap items-center gap-3 text-xs">
              {project.clientName && <span className="font-medium text-slate-700">{project.clientName}</span>}
              <span className="rounded bg-blue-50 px-2 py-0.5 font-medium text-blue-700">{project.status}</span>
              <DeadlineBadge deadline={project.deadline} status={project.status} />
              <div className="flex items-center gap-2 w-44">
                <div className="h-2 flex-1 rounded-full bg-slate-100 overflow-hidden">
                  <div className="h-full bg-primary rounded-full" style={{ width: `${project.progress}%` }} />
                </div>
                <span className="font-semibold text-slate-700">{project.progress}%</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setShowAddMilestone(true)}
              className="inline-flex items-center gap-1.5 rounded-control border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              <Layers className="h-4 w-4" /> Add Milestone
            </button>
            <button
              type="button"
              onClick={() => setShowAddTask(true)}
              className="inline-flex items-center gap-1.5 rounded-control bg-primary px-3.5 py-2 text-sm font-medium text-white hover:opacity-95"
            >
              <Plus className="h-4 w-4" /> Add Task
            </button>
          </div>
        </div>

        {/* Tabs + Plugin ExtensionSlot */}
        <div className="mt-5 flex flex-wrap items-center gap-6 border-t border-slate-100 pt-3 text-sm">
          {(['overview', 'tasks', 'milestones'] as const).map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => setTab(t)}
              className={`pb-1 font-medium capitalize ${
                tab === t ? 'border-b-2 border-primary text-primary' : 'text-slate-500 hover:text-slate-800'
              }`}
            >
              {t}
            </button>
          ))}
          <ExtensionSlot
            name="project.details.tabs"
            context={{ projectId: project.id, clientId: project.clientId, projectStatus: project.status }}
            className="flex items-center gap-4"
          />
        </div>
      </div>

      {/* Tab Content */}
      {tab === 'overview' && (
        <div className="rounded-card border border-slate-200 bg-surface p-5">
          <h2 className="text-base font-semibold text-slate-900 mb-2">Project Description</h2>
          <p className="text-sm text-slate-600">{project.description || 'No description provided.'}</p>
        </div>
      )}

      {tab === 'tasks' && (
        <div className="space-y-3">
          {selectedIds.length > 0 && (
            <div className="flex items-center justify-between rounded-control border border-indigo-200 bg-indigo-50 px-4 py-2 text-sm">
              <span className="font-medium text-indigo-900">{selectedIds.length} tasks selected</span>
              <div className="flex items-center gap-2">
                <select
                  value={batchStatus}
                  onChange={(e) => setBatchStatus(e.target.value)}
                  className="rounded border border-indigo-200 bg-white px-2 py-1 text-xs"
                >
                  {COLUMNS.map((c) => <option key={c.key} value={c.key}>{c.label}</option>)}
                </select>
                <button
                  type="button"
                  onClick={() => batchUpdate.mutate()}
                  className="rounded bg-primary px-3 py-1 text-xs font-medium text-white"
                >
                  Batch Update
                </button>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-4">
            <div className="lg:col-span-3 grid grid-cols-1 gap-4 md:grid-cols-4">
              {COLUMNS.map((col) => {
                const colTasks = project.tasks.filter((t) => t.status === col.key);
                return (
                  <div key={col.key} className="rounded-card border border-slate-200 bg-slate-50/70 p-3 space-y-2.5">
                    <div className="flex items-center justify-between px-1">
                      <span className="text-xs font-semibold text-slate-700">{col.label}</span>
                      <span className="rounded-full bg-slate-200 px-2 py-0.5 text-[11px] font-semibold text-slate-700">
                        {colTasks.length}
                      </span>
                    </div>
                    {colTasks.map((t) => {
                      const checked = selectedIds.includes(t.id);
                      return (
                        <div
                          key={t.id}
                          className="rounded-control border border-slate-200 bg-surface p-3 shadow-sm space-y-2"
                        >
                          <div className="flex items-start justify-between gap-2">
                            <button
                              type="button"
                              onClick={() => setSelectedTask(t)}
                              className="text-left text-sm font-medium text-slate-800 hover:text-primary"
                            >
                              {t.title}
                            </button>
                            <input
                              type="checkbox"
                              checked={checked}
                              onChange={(e) =>
                                setSelectedIds((prev) =>
                                  e.target.checked ? [...prev, t.id] : prev.filter((x) => x !== t.id)
                                )
                              }
                              className="mt-0.5 h-3.5 w-3.5 accent-primary"
                            />
                          </div>
                          <div className="flex items-center justify-between text-[11px]">
                            <span className="rounded bg-slate-100 px-1.5 py-0.5 font-medium text-slate-600">{t.points} pts</span>
                            <div className="flex items-center gap-1">
                              {t.status !== 'Done' && (
                                <button
                                  type="button"
                                  title="Mark Done"
                                  onClick={() => updateTaskStatus.mutate({ taskId: t.id, status: 'Done' })}
                                  className="rounded bg-emerald-50 p-1 text-emerald-700 hover:bg-emerald-100"
                                >
                                  <Check className="h-3 w-3" />
                                </button>
                              )}
                              <select
                                value={t.status}
                                onChange={(e) => updateTaskStatus.mutate({ taskId: t.id, status: e.target.value })}
                                className="rounded border border-slate-200 bg-white px-1 py-0.5 text-[11px]"
                              >
                                {COLUMNS.map((c) => <option key={c.key} value={c.key}>{c.label}</option>)}
                              </select>
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                );
              })}
            </div>

            {/* Right Milestones Sidebar */}
            <div className="rounded-card border border-slate-200 bg-surface p-4 space-y-3 h-fit">
              <h3 className="text-sm font-semibold text-slate-900">Milestones</h3>
              {project.milestones.length === 0 ? (
                <p className="text-xs text-slate-400">Belum ada milestone.</p>
              ) : (
                project.milestones.map((m) => (
                  <div key={m.id} className="space-y-1">
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-medium text-slate-800">{m.title}</span>
                      <span className="font-semibold text-slate-600">{m.progress}%</span>
                    </div>
                    <div className="h-1.5 w-full rounded-full bg-slate-100 overflow-hidden">
                      <div
                        className={`h-full rounded-full ${m.progress === 100 ? 'bg-emerald-600' : 'bg-primary'}`}
                        style={{ width: `${m.progress}%` }}
                      />
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}

      {tab === 'milestones' && (
        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-3">
          {project.milestones.length === 0 ? (
            <p className="text-sm text-slate-400">Belum ada milestone.</p>
          ) : (
            project.milestones.map((m) => (
              <div key={m.id} className="flex items-center justify-between rounded-control border border-slate-100 p-3">
                <div>
                  <div className="font-medium text-slate-900">{m.title}</div>
                  <div className="text-xs text-slate-500">{m.doneTasks} / {m.totalTasks} tasks done</div>
                </div>
                <div className="flex items-center gap-3 w-48">
                  <div className="h-2 flex-1 rounded-full bg-slate-100 overflow-hidden">
                    <div className="h-full bg-primary rounded-full" style={{ width: `${m.progress}%` }} />
                  </div>
                  <span className="text-xs font-semibold">{m.progress}%</span>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* Task Detail Modal with ExtensionSlot */}
      {selectedTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-lg rounded-card bg-surface p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <h3 className="text-base font-bold text-slate-900">{selectedTask.title}</h3>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => deleteTask.mutate(selectedTask.id)}
                  className="text-slate-400 hover:text-red-600"
                  title="Delete Task"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
                <button type="button" onClick={() => setSelectedTask(null)}><X className="h-5 w-5 text-slate-400" /></button>
              </div>
            </div>
            <div className="text-sm text-slate-600">
              Status: <strong>{selectedTask.status}</strong> • Priority: <strong>{selectedTask.priority}</strong> • Points: <strong>{selectedTask.points}</strong>
            </div>
            <ExtensionSlot
              name="task.view.right_panel"
              context={{ taskId: selectedTask.id, projectId: project.id }}
            />
          </div>
        </div>
      )}

      {/* Add Task Modal */}
      {showAddTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-card bg-surface p-6 shadow-xl">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-semibold">Add Task</h3>
              <button type="button" onClick={() => setShowAddTask(false)}><X className="h-5 w-5 text-slate-400" /></button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                createTask.mutate();
              }}
              className="mt-4 space-y-3"
            >
              <input
                required
                placeholder="Task Title"
                value={taskForm.title}
                onChange={(e) => setTaskForm({ ...taskForm, title: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <div className="grid grid-cols-2 gap-3">
                <input
                  type="number"
                  min={1}
                  placeholder="Points"
                  value={taskForm.points}
                  onChange={(e) => setTaskForm({ ...taskForm, points: Number(e.target.value) })}
                  className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
                />
                <select
                  value={taskForm.priority}
                  onChange={(e) => setTaskForm({ ...taskForm, priority: e.target.value })}
                  className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
                >
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Urgent">Urgent</option>
                </select>
              </div>
              <select
                value={taskForm.milestoneId}
                onChange={(e) => setTaskForm({ ...taskForm, milestoneId: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              >
                <option value="">No Milestone</option>
                {project.milestones.map((m) => <option key={m.id} value={m.id}>{m.title}</option>)}
              </select>
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setShowAddTask(false)} className="rounded-control border border-slate-200 px-4 py-2 text-sm">Cancel</button>
                <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Save Task</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Add Milestone Modal */}
      {showAddMilestone && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
          <div className="w-full max-w-md rounded-card bg-surface p-6 shadow-xl">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-semibold">Add Milestone</h3>
              <button type="button" onClick={() => setShowAddMilestone(false)}><X className="h-5 w-5 text-slate-400" /></button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                createMilestone.mutate();
              }}
              className="mt-4 space-y-3"
            >
              <input
                required
                placeholder="Milestone Title (e.g. Design)"
                value={msForm.title}
                onChange={(e) => setMsForm({ ...msForm, title: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <input
                type="date"
                value={msForm.dueDate}
                onChange={(e) => setMsForm({ ...msForm, dueDate: e.target.value })}
                className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setShowAddMilestone(false)} className="rounded-control border border-slate-200 px-4 py-2 text-sm">Cancel</button>
                <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Save Milestone</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
