import type { ExtensionSlotName } from '../../../PluginSdk.Web';
import { useExtensions } from './ExtensionRegistry';

interface ExtensionSlotProps {
  name: ExtensionSlotName;
  context?: Record<string, unknown>;
  className?: string;
}

export function ExtensionSlot({ name, context, className }: ExtensionSlotProps) {
  const { getSlotComponents } = useExtensions();
  const components = getSlotComponents(name);

  if (components.length === 0) return null;

  return (
    <div className={className} data-extension-slot={name}>
      {components.map((reg) => {
        const Comp = reg.component;
        return <Comp key={`${reg.pluginId}:${reg.id}`} {...(context ?? {})} />;
      })}
    </div>
  );
}
