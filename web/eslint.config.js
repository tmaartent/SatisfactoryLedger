// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = defineConfig([
  // The API client is generated from api/openapi.json (npm run generate:api) and git-ignored: not ours to lint.
  { ignores: ['src/app/api/'] },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      // Leaving a field out by destructuring it into a name never used: ({ machines: _, ...factory }) => factory.
      '@typescript-eslint/no-unused-vars': ['error', { ignoreRestSiblings: true }],
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    // A route's providers make an injector the first time the route is used and keep it for the app's life, so a
    // service meant for one page would outlive it (step 123). Put it in the component's providers instead.
    files: ['**/*.routes.ts'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'Property[key.name="providers"]',
          message:
            "A route's providers outlive the route: give a per-page service through the component's providers (primer: A route's providers outlive the route).",
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {},
  },
]);
