import type { Config } from "tailwindcss";
import { light, dark } from "./theme/colors";

// Flattens { primaryHover: '#fff' } -> { 'primary-hover': '#fff' } so Tailwind classes read
// bg-primary-hover, and the dark set gets a '-dark' suffix for the classic
// `bg-x dark:bg-x-dark` pairing — mirrors mobile/tailwind.config.js's flattening exactly.
function toKebab(tokens: Record<string, string>, suffix = ""): Record<string, string> {
  return Object.fromEntries(
    Object.entries(tokens).map(([key, value]) => [
      key.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase() + suffix,
      value,
    ]),
  );
}

export default {
  content: [
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  darkMode: "media",
  theme: {
    extend: {
      colors: {
        ...toKebab(light),
        ...toKebab(dark, "-dark"),
      },
    },
  },
  plugins: [],
} satisfies Config;
