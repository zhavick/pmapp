import React, { createContext, useContext, useEffect, useState } from 'react';

export interface ThemePreset {
  id: string;
  name: string;
  tokens: Record<string, string>;
}

export const THEME_PRESETS: ThemePreset[] = [
  {
    id: 'default-indigo',
    name: 'Default Indigo',
    tokens: {
      '--pm-primary': '243 75% 59%',
      '--pm-sidebar-bg': '222 47% 11%',
      '--pm-sidebar-active': '243 75% 59%',
    },
  },
  {
    id: 'emerald-corporate',
    name: 'Emerald Corporate',
    tokens: {
      '--pm-primary': '160 84% 39%',
      '--pm-sidebar-bg': '164 42% 11%',
      '--pm-sidebar-active': '160 84% 39%',
    },
  },
  {
    id: 'royal-slate',
    name: 'Royal Slate',
    tokens: {
      '--pm-primary': '217 91% 60%',
      '--pm-sidebar-bg': '215 28% 17%',
      '--pm-sidebar-active': '217 91% 60%',
    },
  },
];

const THEME_KEY = 'pmapp_theme_id';

interface ThemeContextValue {
  themeId: string;
  setThemeId: (id: string) => void;
  presets: ThemePreset[];
}

const ThemeContext = createContext<ThemeContextValue>({
  themeId: 'default-indigo',
  setThemeId: () => {},
  presets: THEME_PRESETS,
});

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [themeId, setThemeId] = useState<string>(() => localStorage.getItem(THEME_KEY) || 'default-indigo');

  useEffect(() => {
    const preset = THEME_PRESETS.find((p) => p.id === themeId) ?? THEME_PRESETS[0];
    const root = document.documentElement;
    Object.entries(preset.tokens).forEach(([k, v]) => root.style.setProperty(k, v));
    localStorage.setItem(THEME_KEY, preset.id);
  }, [themeId]);

  return (
    <ThemeContext.Provider value={{ themeId, setThemeId, presets: THEME_PRESETS }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  return useContext(ThemeContext);
}
