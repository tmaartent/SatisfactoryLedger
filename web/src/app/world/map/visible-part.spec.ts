import { coveredBySidebar, coveredSides } from './visible-part';

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

describe('what of the page the sidebar covers, its bar and its open tab, closed or open', () => {
  /** The shell's sidebar as the browser lays it out: its own box, and the open tab's, if a tab is open. */
  const sidebar = (bar: DOMRect, tab: DOMRect | null) => {
    const element = document.createElement('app-sidebar');
    vi.spyOn(element, 'getBoundingClientRect').mockReturnValue(bar);
    if (tab) {
      const open = document.createElement('section');
      open.className = 'tab';
      vi.spyOn(open, 'getBoundingClientRect').mockReturnValue(tab);
      element.append(open);
    }
    document.body.append(element);
  };
  afterEach(() => document.querySelector('app-sidebar')?.remove());

  // A phone's map, 390 wide and 640 high, 116 down from the top of the window.
  const phoneMap = { left: 0, top: 116, right: 390, bottom: 756 };

  it('is the bar of tabs along the bottom of a phone, with every tab closed', () => {
    sidebar(new DOMRect(0, 680, 390, 164), null);

    expect(coveredBySidebar(phoneMap)).toEqual({ top: 0, right: 0, bottom: 76, left: 0 });
  });

  it('is the sheet and the bar under it on a phone, with a tab open', () => {
    const sheet = new DOMRect(0, 436, 390, 300);
    sidebar(new DOMRect(0, 436, 390, 408), sheet);

    expect(coveredBySidebar(phoneMap)).toEqual({ top: 0, right: 0, bottom: 320, left: 0 });
  });

  it('is the open tab lying over the right of a tablet, left of the rail beside the page', () => {
    // A tablet's map; the rail stands beside it, and the tab lies over it, outside the rail's box.
    const map = { left: 16, top: 136, right: 756, bottom: 1120 };
    sidebar(new DOMRect(772, 64, 48, 1116), new DOMRect(432, 64, 340, 1116));

    expect(coveredBySidebar(map)).toEqual({ top: 0, right: 324, bottom: 0, left: 0 });
  });

  it('is nothing when the sidebar stands beside the page', () => {
    const map = { left: 16, top: 136, right: 1000, bottom: 900 };
    sidebar(new DOMRect(1016, 64, 388, 836), new DOMRect(1016, 64, 340, 836));

    expect(coveredBySidebar(map)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
  });

  it("is nothing when the bar and the tab together leave too little to show anything in: there's nothing to centre on", () => {
    // Each leaves 200 of the 300 across; both together only 100.
    const map = { left: 0, top: 0, right: 300, bottom: 600 };
    sidebar(new DOMRect(0, 0, 100, 600), new DOMRect(200, 0, 100, 600));

    expect(coveredBySidebar(map)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
  });

  it('is nothing on a page without the sidebar', () => {
    expect(coveredBySidebar(phoneMap)).toEqual({ top: 0, right: 0, bottom: 0, left: 0 });
  });
});
