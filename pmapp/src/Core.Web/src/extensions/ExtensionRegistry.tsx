import React, { createContext, useContext, useEffect, useState } from 'react';
import type {
  ExtensionSlotName,
  FrontendPluginContext,
  PluginMenuItem,
  PluginRouteDefinition,
  SlotComponentRegistration,
} from '../../../PluginSdk.Web';
import { useAuth } from '../app/AuthContext';
import { apiFetch } from '../lib/api';

interface ActiveExtensionDto {
  pluginId: string;
  name: string;
  version: string;
  bundleUrl: string;
}

interface ExtensionRegistryValue {
  routes: PluginRouteDefinition[];
  addOnMenuItems: PluginMenuItem[];
  getSlotComponents: (slotName: ExtensionSlotName) => SlotComponentRegistration[];
  loadedPluginIds: string[];
}

const ExtensionRegistryContext = createContext<ExtensionRegistryValue>({
  routes: [],
  addOnMenuItems: [],
  getSlotComponents: () => [],
  loadedPluginIds: [],
});

export function ExtensionProvider({ children }: { children: React.ReactNode }) {
  const { user, tenant, activePlugins } = useAuth();
  const [routes, setRoutes] = useState<PluginRouteDefinition[]>([]);
  const [addOnMenuItems, setAddOnMenuItems] = useState<PluginMenuItem[]>([]);
  const [slots, setSlots] = useState<SlotComponentRegistration[]>([]);
  const [loadedPluginIds, setLoadedPluginIds] = useState<string[]>([]);

  useEffect(() => {
    if (!user || !tenant) {
      setRoutes([]);
      setAddOnMenuItems([]);
      setSlots([]);
      setLoadedPluginIds([]);
      return;
    }

    let cancelled = false;

    async function loadActiveBundles() {
      try {
        const active = await apiFetch<ActiveExtensionDto[]>('/api/v1/extensions/active');
        const nextRoutes: PluginRouteDefinition[] = [];
        const nextMenus: PluginMenuItem[] = [];
        const nextSlots: SlotComponentRegistration[] = [];
        const nextLoaded: string[] = [];

        const sdkContext: FrontendPluginContext = {
          registerRoute: (r) => nextRoutes.push(r),
          registerAddOnMenuItem: (m) => nextMenus.push(m),
          registerSlotComponent: (s) => nextSlots.push(s),
        };

        for (const ext of active) {
          try {
            const mod = await import(/* @vite-ignore */ ext.bundleUrl);
            if (typeof mod.register === 'function') {
              mod.register(sdkContext);
              nextLoaded.push(ext.pluginId);
            }
          } catch (err) {
            console.error(`Failed to load frontend bundle for plugin '${ext.pluginId}':`, err);
          }
        }

        if (!cancelled) {
          setRoutes(nextRoutes);
          setAddOnMenuItems(nextMenus.sort((a, b) => (a.order ?? 100) - (b.order ?? 100)));
          setSlots(nextSlots.sort((a, b) => (a.order ?? 100) - (b.order ?? 100)));
          setLoadedPluginIds(nextLoaded);
        }
      } catch {
        // ignore if unauthenticated
      }
    }

    loadActiveBundles();
    return () => {
      cancelled = true;
    };
  }, [user?.id, tenant?.id, activePlugins.join(',')]);

  const getSlotComponents = (slotName: ExtensionSlotName) =>
    slots.filter((s) => s.slotName === slotName && activePlugins.includes(s.pluginId));

  return (
    <ExtensionRegistryContext.Provider value={{ routes, addOnMenuItems, getSlotComponents, loadedPluginIds }}>
      {children}
    </ExtensionRegistryContext.Provider>
  );
}

export function useExtensions() {
  return useContext(ExtensionRegistryContext);
}
