import { afterSwipe } from './sheet';

describe("the sidebar's sheet on a phone", () => {
  it('goes to full height when its handle is swiped up', () => {
    expect(afterSwipe('half', -80)).toBe('full');
  });

  it('goes back to half height when swiped down from full', () => {
    expect(afterSwipe('full', 80)).toBe('half');
  });

  it('closes when swiped down from half height, leaving the bar of tabs', () => {
    expect(afterSwipe('half', 80)).toBe('closed');
  });

  it('switches between half and full height when its handle is tapped', () => {
    expect([afterSwipe('half', 3), afterSwipe('full', -3)]).toEqual(['full', 'half']);
  });
});
