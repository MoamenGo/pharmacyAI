import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      fontFamily: {
        // System fonts only: the app must render fully offline.
        sans: ["Segoe UI", "Tahoma", "Noto Sans Arabic", "Arial", "sans-serif"],
      },
    },
  },
  plugins: [],
};

export default config;
