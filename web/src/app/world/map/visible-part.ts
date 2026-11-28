/** A rectangle on the screen, in pixels from the window's top left, as `getBoundingClientRect` gives it. */
export interface ScreenBox {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** How many pixels of each side of the map something lies over. */
export interface Sides {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

/** Less than this left in view, in either direction, is too little to centre anything in. */
const least = 120;

/**
 * How much of each side of the map the open tab lies over (asked 2026-11-24): a sheet over its lower part on a phone,
 * a tab over its right part on a narrow screen; nothing when it stands beside the map, or when no tab is open. Of the
 * four sides it could be cut from, the one leaving the most in view. Nothing either when that's too little to show
 * anything in, as under a sheet at full height.
 */
export function coveredSides(map: ScreenBox, tab: ScreenBox | null): Sides {
  const none = { top: 0, right: 0, bottom: 0, left: 0 };
  if (
    !tab ||
    tab.left >= map.right ||
    tab.right <= map.left ||
    tab.top >= map.bottom ||
    tab.bottom <= map.top
  )
    return none;
  const [width, height] = [map.right - map.left, map.bottom - map.top];
  const cuts: Sides[] = [
    { ...none, top: tab.bottom - map.top },
    { ...none, right: map.right - tab.left },
    { ...none, bottom: map.bottom - tab.top },
    { ...none, left: tab.right - map.left },
  ];
  const left = (cut: Sides) => [width - cut.left - cut.right, height - cut.top - cut.bottom];
  const best = cuts.reduce((a, b) => {
    const [[aw, ah], [bw, bh]] = [left(a), left(b)];
    return bw * bh > aw * ah ? b : a;
  });
  return tooLittleLeft(map, best) ? none : best;
}

/** Whether what a cut leaves of the map in view is too little, in either direction, to centre anything in. */
function tooLittleLeft(map: ScreenBox, cut: Sides): boolean {
  const width = map.right - map.left - cut.left - cut.right;
  const height = map.bottom - map.top - cut.top - cut.bottom;
  return width < least || height < least;
}

/**
 * How much of each side of a part of the page the sidebar covers, as the browser has laid it out (decided
 * 2026-11-28): whatever of it lies over the page, closed or open. Its own box, the rail or, on a phone, the bar of
 * tabs and the snapshot picker along the bottom with the sheet above them; and the open tab, which on a narrow screen
 * lies over the page outside that box. One read for the area page and the world map. Nothing when they leave too
 * little in view together, as `coveredSides` for one.
 */
export function coveredBySidebar(page: ScreenBox): Sides {
  const sidebar = document.querySelector('app-sidebar');
  const covered = [sidebar, sidebar?.querySelector('.tab')]
    .filter((element) => element != null)
    .map((element) => coveredSides(page, element.getBoundingClientRect()));
  const most = (side: keyof Sides) => Math.max(0, ...covered.map((sides) => sides[side]));
  const cut = {
    top: most('top'),
    right: most('right'),
    bottom: most('bottom'),
    left: most('left'),
  };
  // Too little left between the two, as under one alone: nothing to centre on.
  return tooLittleLeft(page, cut) ? { top: 0, right: 0, bottom: 0, left: 0 } : cut;
}
