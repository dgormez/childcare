/**
 * colors.js — single source of truth for the app's color tokens.
 *
 * Imported by both tailwind.config.js (className usage — bg-background,
 * text-text, etc.) and hooks/useColors.ts (the handful of call sites that
 * need a raw hex value: StatusBar tint, native Image tint). Change a value
 * once, here — never hardcode a hex/Tailwind color anywhere else.
 *
 * Plain CommonJS (not .ts): tailwind.config.js is required directly by Node
 * with no TypeScript transpilation step, so this file has to stay .js.
 *
 * See .specify/memory/design-system.md for the full rationale per token.
 */
const light = {
  background:   "#FAF9F6",
  surface:      "#FFFFFF",
  surfaceSoft:  "#F5F3EE",
  border:       "#E7E2D8",
  text:         "#1F2937",
  textSoft:     "#6B7280",
  placeholder:  "#9CA3AF",

  primary:      "#4F7CAC",
  primaryHover: "#3D638A", // also the verified-AA text-on-light color for primary-hued text/links
  primarySoft:  "#E8EEF4",

  danger:    "#B91C1C",
  dangerBg:  "#FEF2F2",
  warning:   "#F59E0B", // solid banner fill, no separate tint — see design-system.md
  warningFg: "#1C1917", // fixed dark text on the amber fill — same value in both themes
  success:   "#15803D",
  successBg: "#F0FDF4",
  info:      "#0EA5E9", // solid banner fill, distinct hue from primary on purpose

  // Chrome-only per-surface accents (nav highlights, selected-tab underline — never a
  // badge/banner/checkmark, which would read as a status). Caregiver has none by design:
  // "completed action" is success-green's job already; a second color for the same
  // meaning adds confusion, not information, on the surface where glanceability matters most.
  accentDirector: "#5B5FC7",
  accentParent:   "#B8768C",
};

const dark = {
  background:   "#1C1917",
  surface:      "#262220",
  surfaceSoft:  "#302B27",
  border:       "#47403A",
  text:         "#F5F1EA",
  textSoft:     "#A39B8F",
  placeholder:  "#6B7280",

  primary:      "#7CA0C7",
  primaryHover: "#93B2D3", // hover/pressed goes lighter, not darker, against a dark ground
  primarySoft:  "rgba(124,160,199,0.15)",

  danger:    "#FCA5A5",
  dangerBg:  "rgba(127,29,29,0.3)",
  warning:   "#D97706",
  warningFg: "#1C1917",
  success:   "#86EFAC",
  successBg: "rgba(20,83,45,0.3)",
  info:      "#0284C7",

  accentDirector: "#8B8EE0",
  accentParent:   "#D49CB0",
};

module.exports = { light, dark };
