import { coveredSides } from './visible-part';

describe('the part of the map left in view by the open tab', () => {
  // A phone's map, 390 wide and 640 high, 116 down from the top of the window.
  const map = { left: 0, top: 116, right: 390, bottom: 756 };

  it('is above a sheet that covers its lower part', () => {
    const sheet = { left: 0, top: 436, right: 390, bottom: 844 };

    expect(coveredSides(map, sheet)).toEqual({ top: 0, right: 0, bottom: 320, left: 0 });
  });

  it('is left of a tab lying over its right part', () => {
    // A tablet's map, with the tab over its right side, left of the rail.
    const wide = { left: 16, top: 136, right: 756, bottom: 1120 };
    const tab = { left: 432, top: 64, right: 772, bottom: 1180 };

    expect(coveredSides(wide, tab)).toEqual({ top: 0, right: 324, bottom: 0, left: 0 });
  });

  it('is all of it when the tab stands beside the map', () => {
    const tab = { left: 400, top: 64, right: 740, bottom: 844 };

    expect(coveredSides(map, tab)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
  });

  it('is all of it when no tab is open', () => {
    expect(coveredSides(map, null)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
  });

  it("is all of it when a sheet at full height leaves too little to show anything in: there's nothing to centre on", () => {
    const sheet = { left: 0, top: 56, right: 390, bottom: 844 };

    expect(coveredSides(map, sheet)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
  });
});
