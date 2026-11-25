import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { apiFetch } from '../../lib/api';

interface WorkspaceNote {
  id: string;
  title: string;
  content: string;
  isSticky: boolean;
}

interface TodoItem {
  id: string;
  title: string;
  isCompleted: boolean;
}

export function NotesTodoPage() {
  const qc = useQueryClient();
  const [noteTitle, setNoteTitle] = useState('');
  const [noteContent, setNoteContent] = useState('');
  const [todoTitle, setTodoTitle] = useState('');

  const { data: notes = [] } = useQuery({
    queryKey: ['workspace-notes'],
    queryFn: () => apiFetch<WorkspaceNote[]>('/api/v1/workspace/notes'),
  });

  const { data: todos = [] } = useQuery({
    queryKey: ['workspace-todos'],
    queryFn: () => apiFetch<TodoItem[]>('/api/v1/workspace/todos'),
  });

  const createNote = useMutation({
    mutationFn: () => apiFetch('/api/v1/workspace/notes', { method: 'POST', body: JSON.stringify({ title: noteTitle, content: noteContent, isSticky: true }) }),
    onSuccess: () => {
      setNoteTitle('');
      setNoteContent('');
      qc.invalidateQueries({ queryKey: ['workspace-notes'] });
    },
  });

  const deleteNote = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/v1/workspace/notes/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['workspace-notes'] }),
  });

  const createTodo = useMutation({
    mutationFn: () => apiFetch('/api/v1/workspace/todos', { method: 'POST', body: JSON.stringify({ title: todoTitle }) }),
    onSuccess: () => {
      setTodoTitle('');
      qc.invalidateQueries({ queryKey: ['workspace-todos'] });
    },
  });

  const toggleTodo = useMutation({
    mutationFn: (t: TodoItem) => apiFetch(`/api/v1/workspace/todos/${t.id}`, { method: 'PATCH', body: JSON.stringify({ isCompleted: !t.isCompleted }) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['workspace-todos'] }),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">Notes &amp; Personal To-Do</h1>
        <p className="text-sm text-slate-500">Private notes and daily checklist scoped to your workspace.</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Notes */}
        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-4">
          <h2 className="text-base font-semibold text-slate-900">Sticky Notes</h2>
          <form onSubmit={(e) => { e.preventDefault(); if (noteTitle.trim()) createNote.mutate(); }} className="space-y-2">
            <input
              placeholder="Note title..."
              value={noteTitle}
              onChange={(e) => setNoteTitle(e.target.value)}
              className="w-full rounded-control border border-slate-200 px-3 py-2 text-sm"
            />
            <div className="flex gap-2">
              <input
                placeholder="Note content..."
                value={noteContent}
                onChange={(e) => setNoteContent(e.target.value)}
                className="flex-1 rounded-control border border-slate-200 px-3 py-2 text-sm"
              />
              <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Add</button>
            </div>
          </form>
          <div className="space-y-2">
            {notes.map((n) => (
              <div key={n.id} className="flex items-start justify-between rounded-control border border-amber-200 bg-amber-50/70 p-3">
                <div>
                  <div className="text-sm font-semibold text-slate-800">{n.title}</div>
                  {n.content && <div className="text-xs text-slate-600 mt-0.5">{n.content}</div>}
                </div>
                <button type="button" onClick={() => deleteNote.mutate(n.id)} className="text-slate-400 hover:text-red-600">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ))}
          </div>
        </div>

        {/* Personal To-Do */}
        <div className="rounded-card border border-slate-200 bg-surface p-5 space-y-4">
          <h2 className="text-base font-semibold text-slate-900">Personal To-Do</h2>
          <form onSubmit={(e) => { e.preventDefault(); if (todoTitle.trim()) createTodo.mutate(); }} className="flex gap-2">
            <input
              placeholder="Add personal to-do..."
              value={todoTitle}
              onChange={(e) => setTodoTitle(e.target.value)}
              className="flex-1 rounded-control border border-slate-200 px-3 py-2 text-sm"
            />
            <button type="submit" className="rounded-control bg-primary px-4 py-2 text-sm font-medium text-white">Add</button>
          </form>
          <div className="space-y-2">
            {todos.map((t) => (
              <label key={t.id} className="flex items-center gap-3 rounded-control border border-slate-100 px-3 py-2 text-sm cursor-pointer hover:bg-slate-50">
                <input
                  type="checkbox"
                  checked={t.isCompleted}
                  onChange={() => toggleTodo.mutate(t)}
                  className="h-4 w-4 accent-primary"
                />
                <span className={t.isCompleted ? 'line-through text-slate-400' : 'text-slate-800'}>{t.title}</span>
              </label>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
