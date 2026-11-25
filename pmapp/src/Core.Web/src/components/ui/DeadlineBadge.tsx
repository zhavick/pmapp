import { Calendar } from 'lucide-react';

export function DeadlineBadge({ deadline, status }: { deadline?: string | null; status?: string }) {
  if (!deadline) return null;

  const todayStr = new Date().toISOString().slice(0, 10);
  const isCompleted = status === 'Completed' || status === 'Done';

  let colorClass = 'bg-slate-100 text-slate-600';
  if (!isCompleted) {
    if (deadline < todayStr) {
      colorClass = 'bg-red-100 text-red-700';
    } else if (deadline === todayStr) {
      colorClass = 'bg-amber-100 text-amber-800';
    }
  }

  return (
    <span className={`inline-flex items-center gap-1 rounded px-2 py-0.5 text-xs font-medium ${colorClass}`}>
      <Calendar className="h-3 w-3" />
      {deadline}
    </span>
  );
}
