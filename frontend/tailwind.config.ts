/*
 * Tailwind config — token map จาก CSS custom properties ใน globals.css
 * SSoT: docs/AI/DESIGN.md
 * ห้ามเพิ่ม color ที่ไม่มีใน DESIGN.md
 */
import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        // Brand
        navy:       "var(--navy)",
        blue:       "var(--blue)",
        "blue-t":   "var(--blue-t)",
        blue2:      "var(--blue2)",
        "blue2-t":  "var(--blue2-t)",
        // Semantic
        emerald:       "var(--emerald)",
        "emerald-t":   "var(--emerald-t)",
        amber:         "var(--amber)",
        "amber-t":     "var(--amber-t)",
        rose:          "var(--rose)",
        "rose-t":      "var(--rose-t)",
        rose2:         "var(--rose2)",
        "rose2-t":     "var(--rose2-t)",
        sky:           "var(--sky)",
        "sky-t":       "var(--sky-t)",
        // Nav / Sidebar
        "nav-active":       "var(--nav-active)",
        "nav-label":        "var(--nav-label)",
        "nav-chip-bg":      "var(--nav-chip-bg)",
        "nav-chip-color":   "var(--nav-chip-color)",
        "nav-section":      "var(--nav-section)",
        "sidebar-zone-bg":  "var(--sidebar-zone-bg)",
        // Neutrals
        t1:      "var(--t1)",
        t2:      "var(--t2)",
        t3:      "var(--t3)",
        border:  "var(--border)",
        sep:     "var(--sep)",
        bg:      "var(--bg)",
        surf:    "var(--surf)",
      },
      fontFamily: {
        sans: ["var(--font-sans)", "system-ui", "sans-serif"],
        mono: ["var(--font-mono)", "ui-monospace", "monospace"],
      },
      borderRadius: {
        // map ตาม --r tokens จาก DESIGN.md
        DEFAULT: "var(--r)",    // 10px — cards
        btn:     "var(--rb)",   // 8px  — buttons, inputs
        icon:    "var(--ri)",   // 7px  — icon containers
        pill:    "var(--rp)",   // 6px  — badges, pills
      },
      fontSize: {
        // map จาก --text-* tokens (ใช้ใน className: text-sm-token ฯลฯ)
        "t-xs":   "var(--text-xs)",   // 11px — badges, chips
        "t-sm":   "var(--text-sm)",   // 12px — secondary, mono
        "t-base": "var(--text-base)", // 13px — body, buttons
        "t-md":   "var(--text-md)",   // 13.5px — inputs
        "t-lg":   "var(--text-lg)",   // 14px — card titles
        "t-xl":   "var(--text-xl)",   // 15px — section titles
        "t-2xl":  "var(--text-2xl)",  // 17px — view titles
        "t-3xl":  "var(--text-3xl)",  // 18px — page titles
        "t-kpi":  "var(--text-kpi)",  // 26px — KPI numbers
      },
      transitionDuration: {
        DEFAULT: "120ms",
        md:      "180ms",
      },
      zIndex: {
        topbar: "var(--z-topbar)",
        modal:  "var(--z-modal)",
        toast:  "var(--z-toast)",
      },
      spacing: {
        sidebar: "var(--sw)",
        topbar:  "var(--topbar-h)",
      },
      // ไม่มี boxShadow — design system ไม่ใช้ shadow (ใช้ border เท่านั้น)
    },
  },
  plugins: [],
};

export default config;
