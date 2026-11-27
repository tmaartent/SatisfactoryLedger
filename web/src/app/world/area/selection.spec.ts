import { choicesTop, inBox, isTap, rectBetween, toggle } from './selection';

describe('selecting machines for a factory', () => {
  describe('a click', () => {
    it('adds a machine that is not selected yet', () => {
      expect([...toggle(new Set(['a']), 'b')]).toEqual(['a', 'b']);
    });

    it('takes out a machine that is selected', () => {
      expect([...toggle(new Set(['a', 'b']), 'a')]).toEqual(['b']);
    });

    it('leaves the selection it started from as it was', () => {
      const before = new Set(['a']);
      toggle(before, 'b');

      expect([...before]).toEqual(['a']);
    });
  });

  describe('a finger let go of on a touch screen, selecting', () => {
    it('is a tap when it moved no more than a click may, so the machine under it is added or taken out', () => {
      expect(isTap({ left: 100, top: 100, right: 102, bottom: 103 })).toBe(true);
    });

    it('is a box when it moved further, either way', () => {
      expect(isTap({ left: 100, top: 100, right: 105, bottom: 100 })).toBe(false);
      expect(isTap({ left: 100, top: 100, right: 100, bottom: 105 })).toBe(false);
    });
  });

  describe('what to do with a box drawn by touch, offered beside it', () => {
    // The part of the view in sight, from its top: 500 pixels of it; the choices take 56.
    const seen = { top: 0, bottom: 500 };

    it('stands just below the box, where the finger let go', () => {
      expect(choicesTop({ left: 50, top: 100, right: 200, bottom: 300 }, seen)).toBe(308);
    });

    it('stands above the box when there is no room below it', () => {
      expect(choicesTop({ left: 50, top: 200, right: 200, bottom: 460 }, seen)).toBe(136);
    });

    it('counts only the part of the view in sight: below that, the choices would lie off the screen', () => {
      // The view runs on below the screen, and the box was drawn near the screen's bottom edge.
      const scrolled = { top: 0, bottom: 400 };

      expect(choicesTop({ left: 50, top: 150, right: 200, bottom: 380 }, scrolled)).toBe(86);
    });

    it('counts only the part in sight at the top too, as when the view lies partly under the toolbar', () => {
      const under = { top: 120, bottom: 500 };

      expect(choicesTop({ left: 50, top: 150, right: 200, bottom: 470 }, under)).toBe(128);
    });

    it('stands on the side with more room, inside what is in sight, when there is room on neither side', () => {
      expect(choicesTop({ left: 50, top: 40, right: 200, bottom: 470 }, seen)).toBe(8);
      expect(choicesTop({ left: 50, top: 20, right: 200, bottom: 450 }, seen)).toBe(436);
    });
  });

  describe('a box dragged on screen', () => {
    it('is the same whichever corner the drag starts from', () => {
      expect(rectBetween({ x: 300, y: 50 }, { x: 100, y: 200 })).toEqual({
        left: 100,
        top: 50,
        right: 300,
        bottom: 200,
      });
    });

    const box = { left: 100, top: 100, right: 200, bottom: 200 };
    const onScreen = [
      { id: 'inside', x: 150, y: 150 },
      { id: 'on-the-edge', x: 200, y: 100 },
      { id: 'outside', x: 250, y: 150 },
    ];

    it('takes in every machine whose middle lies inside it, edges included', () => {
      expect(inBox(onScreen, box)).toEqual(['inside', 'on-the-edge']);
    });
  });
});
