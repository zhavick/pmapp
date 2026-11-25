import { jsxs as n, jsx as r } from "react/jsx-runtime";
import { useState as s } from "react";
function u({ projectId: t }) {
  const [e, i] = s(0);
  return /* @__PURE__ */ n("div", { id: "plugin-widget", children: [
    /* @__PURE__ */ n("span", { children: [
      "Plugin Loaded for ",
      t,
      " — Count: ",
      e
    ] }),
    /* @__PURE__ */ r("button", { onClick: () => i((o) => o + 1), children: "Increment" })
  ] });
}
function l(t) {
  t.registerSlot("project.details.tabs", u);
}
export {
  u as PluginWidget,
  l as register
};
