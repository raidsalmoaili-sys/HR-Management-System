/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./Pages/**/*.cshtml",
    "./wwwroot/js/**/*.js",
  ],
  theme: {
    extend: {
      colors: {
        primary:        'var(--hr-primary)',
        'primary-light':'var(--hr-primary-light)',
        'primary-deep': 'var(--hr-primary-deep)',
        accent:         'var(--hr-accent)',
        'accent-light': 'var(--hr-accent-light)',
        'accent-deep':  'var(--hr-accent-deep)',
        bg:    '#f0f2f7',
        ink:   '#1a2744',
        muted: '#7f8c9a',
        scopeGlobal:  '#8e44ad',
        scopeOrg:     '#1a2744',
        scopeCompany: '#c8a84b',
        scopeBranch:  '#16a085',
      },
      fontFamily: {
        tajawal: ['Tajawal', 'sans-serif'],
        inter:   ['Inter', 'sans-serif'],
      },
      borderRadius: {
        card: '16px',
        ctrl: '10px',
      },
    },
  },
  plugins: [
    require('tailwindcss-rtl'),
  ],
};
