import { sceneBackground } from './scene-background';

describe("a 3D view's background", () => {
  it("takes the colour the browser worked out for the stage's background", () => {
    expect(sceneBackground('rgb(14, 14, 17)')).toBe('rgb(14, 14, 17)');
  });

  // Material's colours are light-dark(…) with the site's dark colour scheme; three.js can't read that, and stayed white.
  it('never takes a colour three.js cannot read', () => {
    expect(sceneBackground('light-dark(#fff, #0e0e11)')).toBe('#1b1f24');
  });

  it('falls back to a dark grey when the stage has no colour of its own', () => {
    expect(sceneBackground('rgba(0, 0, 0, 0)')).toBe('#1b1f24');
    expect(sceneBackground('')).toBe('#1b1f24');
  });
});
