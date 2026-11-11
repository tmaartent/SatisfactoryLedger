import { boxSelect, rectBetween, toggle } from './selection';

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

  describe('a box dragged on screen', () => {
    it('is the same whichever corner the drag starts from', () => {
      expect(rectBetween({ x: 300, y: 50 }, { x: 100, y: 200 })).toEqual({ left: 100, top: 50, right: 300, bottom: 200 });
    });

    const box = { left: 100, top: 100, right: 200, bottom: 200 };
    const onScreen = [
      { id: 'inside', x: 150, y: 150 },
      { id: 'on-the-edge', x: 200, y: 100 },
      { id: 'outside', x: 250, y: 150 },
    ];

    it('adds every machine inside it, with shift', () => {
      expect([...boxSelect(new Set(['kept']), onScreen, box, 'add')]).toEqual(['kept', 'inside', 'on-the-edge']);
    });

    it('takes out every machine inside it, with alt', () => {
      expect([...boxSelect(new Set(['inside', 'outside']), onScreen, box, 'remove')]).toEqual(['outside']);
    });
  });
});
