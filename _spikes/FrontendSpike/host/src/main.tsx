import React from 'react';
import ReactDOM from 'react-dom/client';
import * as JsxRuntime from 'react/jsx-runtime';

// Expose shared React singletons for dynamic ES module plugins
(window as any).__PM_SHARED__ = { React, ReactDOM, JsxRuntime };

function createBlobModule(code: string) {
  return URL.createObjectURL(new Blob([code], { type: 'application/javascript' }));
}

// Register import map dynamically before loading plugin
const reactUrl = createBlobModule(`
  const R = window.__PM_SHARED__.React;
  export default R;
  export const { useState, useEffect, useContext, useRef, useMemo, useCallback, createContext } = R;
`);
const jsxUrl = createBlobModule(`
  const J = window.__PM_SHARED__.JsxRuntime;
  export const { jsx, jsxs, Fragment } = J;
`);

const importMapScript = document.createElement('script');
importMapScript.type = 'importmap';
importMapScript.textContent = JSON.stringify({
  imports: {
    react: reactUrl,
    'react/jsx-runtime': jsxUrl,
  },
});
document.head.appendChild(importMapScript);

function App() {
  const [PluginComp, setPluginComp] = React.useState<React.ComponentType<any> | null>(null);
  const [status, setStatus] = React.useState('loading');

  React.useEffect(() => {
    const pluginUrl = '/plugin/index.es.js';
    import(/* @vite-ignore */ pluginUrl)
      .then((mod) => {
        const slots: Record<string, React.ComponentType<any>> = {};
        mod.register({
          registerSlot: (name: string, comp: React.ComponentType<any>) => {
            slots[name] = comp;
          },
        });
        setPluginComp(() => slots['project.details.tabs']);
        setStatus('loaded');
      })
      .catch((err) => {
        setStatus('error: ' + String(err));
      });
  }, []);

  return (
    <div>
      <h1>Spike S3 Host ({status})</h1>
      {PluginComp ? <PluginComp projectId="PRJ-101" /> : <p>Waiting...</p>}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(<App />);
