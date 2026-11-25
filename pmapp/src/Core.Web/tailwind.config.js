/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: 'hsl(var(--pm-primary))',
        sidebar: 'hsl(var(--pm-sidebar-bg))',
        canvas: 'hsl(var(--pm-bg-canvas))',
        surface: 'hsl(var(--pm-bg-surface))',
      },
      borderRadius: {
        card: 'var(--pm-radius-card)',
        control: 'var(--pm-radius-control)',
      },
    },
  },
  plugins: [],
};
