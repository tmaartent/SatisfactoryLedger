import { expect, test } from '@playwright/test';
import Ajv2020 from 'ajv/dist/2020';
import addFormats from 'ajv-formats';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import recorded from './fixtures/api.json' with { type: 'json' };

/**
 * The recorded answers (e2e/fixtures/api.json) still fit the API (step 96): each is checked against the schema
 * api/openapi.json gives for its path and status. The build writes that file from the API's code, and CI fails when it
 * changes uncommitted; this fails when the API changed and the answers weren't recorded again. Runs once, not per
 * device: it needs no browser.
 */
interface Operation {
  responses: Record<string, { content?: Record<string, { schema: object }> }>;
}

const api: {
  paths: Record<string, Record<string, Operation>>;
  components: object;
} = JSON.parse(readFileSync(join(__dirname, '../../api/openapi.json'), 'utf8'));

// The API's schemas, so a $ref to #/components/schemas/… resolves. OpenAPI's number formats (double, int32) say how
// a number is stored, which JSON doesn't tell: any number fits them.
const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats(ajv);
for (const format of ['double', 'float', 'int32', 'int64']) ajv.addFormat(format, true);
ajv.addSchema({ $id: 'openapi.json', components: api.components });

/** The API's path that a recorded one is, by its template: /snapshots/{id}/map takes any id. */
function operationOf(
  method: string,
  path: string,
): { template: string; operation: Operation } | null {
  for (const [template, operations] of Object.entries(api.paths)) {
    const pattern = new RegExp(`^${template.replace(/\{[^}]+\}/g, '[^/]+')}$`);
    const operation = operations[method.toLowerCase()];
    if (pattern.test(path) && operation) return { template, operation };
  }
  return null;
}

/** A schema's $refs pointed at the API's schemas, added above under openapi.json. */
function againstApi(schema: object): object {
  return JSON.parse(
    JSON.stringify(schema).replaceAll('"#/components/', '"openapi.json#/components/'),
  );
}

const answers = Object.entries<{ status: number; body: unknown }>(recorded.answers);

test.describe('the recorded API answers', () => {
  for (const [key, answer] of answers) {
    test(`fit the API: ${key}`, () => {
      const [method, url] = key.split(' ');
      const path = url.split('?')[0];
      const found = operationOf(method, path);
      expect(found, `${key} isn't in api/openapi.json any more`).not.toBeNull();

      const schema =
        found!.operation.responses[String(answer.status)]?.content?.['application/json']?.schema;
      expect(schema, `${found!.template} answers ${answer.status} without a schema`).toBeDefined();
      const valid = ajv.validate(againstApi(schema!), answer.body);
      expect(
        valid
          ? []
          : ajv.errors!.map((error) => `${error.instancePath || '(the body)'} ${error.message}`),
        `${key} no longer fits the API: record again with npm run e2e:record`,
      ).toEqual([]);
    });
  }
});
