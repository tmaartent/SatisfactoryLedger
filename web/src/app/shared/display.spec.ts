import { classNameAsWords, perMinute, percent, positionInMetres } from './display';

describe('display', () => {
  it('names a building by its class name without the prefix and suffix', () => {
    expect(classNameAsWords('Build_ConstructorMk1_C')).toBe('Constructor Mk1');
  });

  it('names an item it has no catalog name for by its class name', () => {
    expect(classNameAsWords('Desc_IronPlate_C')).toBe('Iron Plate');
  });

  it('keeps a recipe name readable', () => {
    expect(classNameAsWords('Recipe_Alternate_PureIronIngot_C')).toBe('Alternate Pure Iron Ingot');
  });

  it('shows a position in metres, as the game does', () => {
    expect(positionInMetres({ x: 12345, y: -500, z: 49 })).toBe('123, −5, 0 m');
  });

  it('shows no position as a dash', () => {
    expect(positionInMetres(null)).toBe('—');
  });

  it('shows utilisation as a whole percentage', () => {
    expect(percent(0.4567)).toBe('46%');
  });

  it('never shows a machine below 100% as 100%', () => {
    expect(percent(0.9984)).toBe('99.8%');
    expect(percent(0.99999)).toBe('99.9%');
  });

  it('shows a machine at full speed as 100%', () => {
    expect(percent(1)).toBe('100%');
  });

  it('shows a rate per minute with at most two decimals', () => {
    expect(perMinute(22.5)).toBe('22.5/min');
    expect(perMinute(13.33333)).toBe('13.33/min');
  });
});
