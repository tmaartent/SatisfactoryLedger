import { classNameAsWords } from '../shared/display';
import { limitParts, limitText } from './limit-text';

describe('limitText', () => {
  const name = (item: string | null | undefined) => (item ? classNameAsWords(item) : '—');

  it('names every input a starved machine is short of, the worst first, with the share it is offered', () => {
    const text = limitText({ limit: 'starved', limitingItem: 'Desc_OreUranium_C', starvedOf: { Desc_OreUranium_C: 0.5, Desc_Sulfur_C: 0.8 }, blockedOn: {} }, name);

    expect(text).toBe('Not enough Ore Uranium (50%), Sulfur (80%)');
  });

  it('names every product that cannot all leave, with the share that can', () => {
    const text = limitText(
      { limit: 'outputBlocked', limitingItem: 'Desc_UraniumCell_C', starvedOf: {}, blockedOn: { Desc_UraniumCell_C: 0.76, Desc_FluidCanister_C: 0.5 } },
      name,
    );

    expect(text).toBe("Can't all leave: Fluid Canister (50%), Uranium Cell (76%)");
  });

  it('falls back to the one limiting item for an analysis without the lists', () => {
    expect(limitText({ limit: 'starved', limitingItem: 'Desc_Coal_C' }, name)).toBe('Not enough Coal');
  });

  it('says a machine on stock what it runs on, and how long it lasts', () => {
    const text = limitText({ limit: 'none', limitingItem: null, onStock: 'Desc_Coal_C', stockMinutes: 42.4 }, name);

    expect(text).toBe('On stock: Coal, lasts 42 min');
  });

  it('says nothing for a machine running on its supply', () => {
    expect(limitText({ limit: 'none', limitingItem: null }, name)).toBe('');
  });

  describe('as parts, so each item can be shown with its icon', () => {
    it('gives the words before the items, and the items with their shares, worst first', () => {
      const parts = limitParts({ limit: 'outputBlocked', limitingItem: null, blockedOn: { Desc_AluminaSolution_C: 0.06, Desc_Water_C: 0.5 } });

      expect(parts).toEqual({
        before: "Can't all leave: ",
        items: [{ item: 'Desc_AluminaSolution_C', share: '6%' }, { item: 'Desc_Water_C', share: '50%' }],
        after: '',
      });
    });

    it('gives the words after the item too, when the sentence goes on past it', () => {
      expect(limitParts({ limit: 'none', limitingItem: null, onStock: 'Desc_Coal_C', stockMinutes: 42.4 })).toEqual({
        before: 'On stock: ',
        items: [{ item: 'Desc_Coal_C', share: null }],
        after: ', lasts 42 min',
      });
    });
  });
});
