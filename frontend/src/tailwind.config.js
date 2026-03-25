/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}", // Dấu ** giúp quét mọi thư mục con như app, horizon, styles...
  ],
  theme: {
    extend: {
      colors: {
        'garage-beige': '#DED8CD',
        'garage-dark': '#1A2C1C',
        'garage-gold': '#B8975A',
      },
      fontFamily: {
        serif: ['Playfair Display', 'serif'],
        sans: ['Inter', 'sans-serif'],
      },
    },
  },
  plugins: [],
}