import React from 'react';
import ReactDOM from 'react-dom/client';
import { initSharedPluginBridge } from './extensions/sharedBridge';
import { App } from './App';
import './index.css';

// Expose shared React singletons before any dynamic plugin bundle loads
initSharedPluginBridge();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
