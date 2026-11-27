/** When the stage gives no colour three.js can read: the dark grey of the site's surfaces. */
const fallback = '#1b1f24';

/**
 * The colour a 3D view's scene is cleared with, from the stage's computed background colour (asked 2026-11-20: it was
 * white). The site's dark colour scheme makes Material's colours `light-dark(…)`, which three.js can't read; the
 * browser's computed value is plain `rgb(…)`. A transparent or unreadable colour gives the fallback.
 */
export function sceneBackground(computed: string): string {
  const colour = computed.trim();
  if (/^rgb\(/.test(colour) || /^#[0-9a-f]{3,8}$/i.test(colour)) return colour;
  return fallback;
}
