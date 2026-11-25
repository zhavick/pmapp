import React, { createContext, useContext, useState } from 'react';
import { RotateCcw, X } from 'lucide-react';

interface UndoToastState {
  message: string;
  onUndo: () => Promise<void> | void;
}

const UndoToastContext = createContext<(msg: string, onUndo: () => Promise<void> | void) => void>(() => {});

export function UndoToastProvider({ children }: { children: React.ReactNode }) {
  const [toast, setToast] = useState<UndoToastState | null>(null);
  const [timerId, setTimerId] = useState<number | null>(null);

  const showUndoToast = (message: string, onUndo: () => Promise<void> | void) => {
    if (timerId) window.clearTimeout(timerId);
    setToast({ message, onUndo });
    const id = window.setTimeout(() => setToast(null), 10_000);
    setTimerId(id);
  };

  const handleUndo = async () => {
    if (!toast) return;
    if (timerId) window.clearTimeout(timerId);
    const fn = toast.onUndo;
    setToast(null);
    await fn();
  };

  return (
    <UndoToastContext.Provider value={showUndoToast}>
      {children}
      {toast && (
        <div className="fixed bottom-5 right-5 z-50 flex items-center gap-3 rounded-control bg-slate-900 px-4 py-3 text-sm text-white shadow-lg">
          <span>{toast.message}</span>
          <button
            type="button"
            onClick={handleUndo}
            className="inline-flex items-center gap-1 rounded bg-indigo-500 px-2.5 py-1 text-xs font-semibold text-white hover:bg-indigo-400"
          >
            <RotateCcw className="h-3.5 w-3.5" />
            Undo (10s)
          </button>
          <button type="button" onClick={() => setToast(null)} className="text-slate-400 hover:text-white">
            <X className="h-4 w-4" />
          </button>
        </div>
      )}
    </UndoToastContext.Provider>
  );
}

export function useUndoToast() {
  return useContext(UndoToastContext);
}
