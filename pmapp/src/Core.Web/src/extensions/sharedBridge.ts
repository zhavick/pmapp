import React from 'react';
import ReactDOM from 'react-dom/client';
import * as JsxRuntime from 'react/jsx-runtime';
import * as ReactRouterDom from 'react-router-dom';
import * as TanStackQuery from '@tanstack/react-query';

declare global {
  interface Window {
    __PM_SHARED__: {
      React: typeof React;
      ReactDOM: typeof ReactDOM;
      JsxRuntime: typeof JsxRuntime;
      ReactRouterDom: typeof ReactRouterDom;
      TanStackQuery: typeof TanStackQuery;
    };
  }
}

export function initSharedPluginBridge() {
  window.__PM_SHARED__ = {
    React,
    ReactDOM,
    JsxRuntime,
    ReactRouterDom,
    TanStackQuery,
  };
}
