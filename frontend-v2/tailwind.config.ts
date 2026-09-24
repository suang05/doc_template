import type { Config } from "tailwindcss";
import { tokens } from "./src/tokens";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      borderRadius: {
        'sm': tokens.radius.sm,
        'DEFAULT': tokens.radius.md,
        'md': tokens.radius.md,
      },
      colors: {
        canvas: tokens.colors.canvas,
        surface: tokens.colors.surface,
        surfaceSubtle: tokens.colors.surfaceSubtle,
        border: tokens.colors.border,
        borderFocus: tokens.colors.borderFocus,
        primary: {
          DEFAULT: tokens.colors.primary,
        },
        primaryLight: tokens.colors.primaryLight,
        primaryDark: tokens.colors.primaryDark,
        textPrimary: tokens.colors.textPrimary,
        textSecondary: tokens.colors.textSecondary,
        textMuted: tokens.colors.textMuted,
        cat: {
          contract: tokens.categories.contract.primary,
          financial: tokens.categories.financial.primary,
          official: tokens.categories.official.primary,
          hr: tokens.categories.hr.primary,
          operations: tokens.categories.operations.primary,
        },
      },
      fontFamily: {
        sans: ["'Sarabun'", "'Inter'", "-apple-system", "BlinkMacSystemFont", "sans-serif"],
        mono: ["ui-monospace", "SFMono-Regular", "Menlo", "Monaco", "Consolas", "monospace"],
      },
      boxShadow: {
        'sm': '0 1px 2px 0 rgba(15, 23, 42, 0.04)',
        'DEFAULT': '0 1px 3px 0 rgba(15, 23, 42, 0.06), 0 1px 2px -1px rgba(15, 23, 42, 0.06)',
        'md': '0 4px 6px -1px rgba(15, 23, 42, 0.08), 0 2px 4px -2px rgba(15, 23, 42, 0.06)',
      },
    },
  },
  plugins: [],
};

export default config;
