import { dirname } from "path";
import { fileURLToPath } from "url";
import { FlatCompat } from "@eslint/eslintrc";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  {
    ignores: ["src/lib/api/schema.d.ts"],
  },
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    linterOptions: {
      reportUnusedDisableDirectives: "error",
    },
    rules: {
      "@typescript-eslint/no-unused-vars": [
        "error",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
        },
      ],
      // Phase 3 debt cap: keep @typescript-eslint/no-explicit-any tracked at warn with --max-warnings=24 in CI
      "@typescript-eslint/no-explicit-any": "warn",
      "@typescript-eslint/naming-convention": [
        "error",
        {
          selector: "typeLike",
          format: ["PascalCase"],
        },
        {
          selector: "function",
          format: ["camelCase", "PascalCase"],
        },
        {
          selector: "variable",
          format: ["camelCase", "PascalCase", "UPPER_CASE"],
          leadingUnderscore: "allow",
        },
      ],
      "react/no-danger": "error",
      "no-restricted-properties": [
        "warn",
        {
          object: "localStorage",
          message:
            "TRAP / Security Guard: Never persist API keys or auth secrets in localStorage; keep preferences server-side or in React state.",
        },
      ],
    },
  },
];

export default eslintConfig;
