import type { ComponentType } from 'react';

export type ExtensionSlotName =
  | 'sidebar.addons'
  | 'dashboard.widgets'
  | 'topbar.quick_add'
  | 'project.details.tabs'
  | 'client.details.tabs'
  | 'task.view.right_panel'
  | 'settings.tenant.tabs';

export interface PluginMenuItem {
  id: string;
  pluginId: string;
  label: string;
  path: string;
  iconName?: string;
  requiredPermission?: string;
  order?: number;
}

export interface PluginRouteDefinition {
  pluginId: string;
  path: string;
  component: ComponentType<any>;
  requiredPermission?: string;
}

export interface SlotComponentRegistration {
  pluginId: string;
  slotName: ExtensionSlotName;
  id: string;
  title?: string;
  component: ComponentType<any>;
  requiredPermission?: string;
  order?: number;
}

export interface FrontendPluginContext {
  registerRoute: (route: PluginRouteDefinition) => void;
  registerAddOnMenuItem: (item: PluginMenuItem) => void;
  registerSlotComponent: (registration: SlotComponentRegistration) => void;
}
