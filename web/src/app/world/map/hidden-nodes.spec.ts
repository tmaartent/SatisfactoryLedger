import { HiddenNodes } from './hidden-nodes';

describe('the kinds of resource node hidden on the map', () => {
  beforeEach(() => localStorage.clear());

  it('hides none at first', () => {
    expect(new HiddenNodes().hidden().size).toBe(0);
  });

  it('hides a kind toggled, and shows it again when toggled again', () => {
    const hidden = new HiddenNodes();

    hidden.toggle('Desc_OreIron_C:pure');
    expect([...hidden.hidden()]).toEqual(['Desc_OreIron_C:pure']);

    hidden.toggle('Desc_OreIron_C:pure');
    expect(hidden.hidden().size).toBe(0);
  });

  it('remembers what is hidden when the site is opened again', () => {
    new HiddenNodes().toggle('Desc_Coal_C:impure');

    expect([...new HiddenNodes().hidden()]).toEqual(['Desc_Coal_C:impure']);
  });

  it('hides all of the kinds given at once, or shows them all', () => {
    const hidden = new HiddenNodes();

    hidden.hideAll(['Desc_Coal_C:impure', 'Desc_OreIron_C:pure']);
    expect(hidden.hidden().size).toBe(2);

    hidden.showAll();
    expect(hidden.hidden().size).toBe(0);
  });
});
