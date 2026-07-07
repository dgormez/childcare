const { light, dark } = require('./theme/colors');

// Flattens { primaryHover: '#fff' } -> { 'primary-hover': '#fff' } so Tailwind classes read
// bg-primary-hover, and the dark set gets a '-dark' suffix for the classic
// `bg-x dark:bg-x-dark` pairing (darkMode: 'media' reacts to the OS scheme automatically).
function toKebab(tokens, suffix = '') {
  return Object.fromEntries(
    Object.entries(tokens).map(([key, value]) => [
      key.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase() + suffix,
      value,
    ])
  );
}

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./app/**/*.{js,jsx,ts,tsx}', './components/**/*.{js,jsx,ts,tsx}'],
  presets: [require('nativewind/preset')],
  darkMode: 'media',
  theme: {
    extend: {
      colors: {
        ...toKebab(light),
        ...toKebab(dark, '-dark'),
      },
    },
  },
  plugins: [],
};
