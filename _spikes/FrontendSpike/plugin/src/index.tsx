import { useState } from 'react';

export function PluginWidget({ projectId }: { projectId: string }) {
  const [count, setCount] = useState(0);
  return (
    <div id="plugin-widget">
      <span>Plugin Loaded for {projectId} — Count: {count}</span>
      <button onClick={() => setCount((c) => c + 1)}>Increment</button>
    </div>
  );
}

export function register(ctx: { registerSlot: (name: string, comp: any) => void }) {
  ctx.registerSlot('project.details.tabs', PluginWidget);
}
