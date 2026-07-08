/**
 * colors.ts — web's consumer of the same color tokens as mobile/theme/colors.js.
 *
 * Kept as a separate TypeScript file rather than importing mobile/theme/colors.js directly:
 * mobile/ and web/ are separate npm projects with no shared-package tooling in this repo yet
 * (design-decisions.md). Values must be kept in sync by hand with mobile/theme/colors.js until
 * a real shared-package setup is worth introducing.
 *
 * See .specify/memory/design-system.md for the full rationale per token.
 */

export const light = {
  background: "#FAF9F6",
  surface: "#FFFFFF",
  surfaceSoft: "#F5F3EE",
  border: "#E7E2D8",
  text: "#1F2937",
  textSoft: "#6B7280",
  placeholder: "#9CA3AF",

  primary: "#4F7CAC",
  primaryHover: "#3D638A",
  primarySoft: "#E8EEF4",

  danger: "#B91C1C",
  dangerBg: "#FEF2F2",
  warning: "#F59E0B",
  warningFg: "#1C1917",
  success: "#15803D",
  successBg: "#F0FDF4",
  info: "#0EA5E9",

  accentDirector: "#5B5FC7",
  accentParent: "#B8768C",
};

export const dark = {
  background: "#1C1917",
  surface: "#262220",
  surfaceSoft: "#302B27",
  border: "#47403A",
  text: "#F5F1EA",
  textSoft: "#A39B8F",
  placeholder: "#6B7280",

  primary: "#7CA0C7",
  primaryHover: "#93B2D3",
  primarySoft: "rgba(124,160,199,0.15)",

  danger: "#FCA5A5",
  dangerBg: "rgba(127,29,29,0.3)",
  warning: "#D97706",
  warningFg: "#1C1917",
  success: "#86EFAC",
  successBg: "rgba(20,83,45,0.3)",
  info: "#0284C7",

  accentDirector: "#8B8EE0",
  accentParent: "#D49CB0",
};
