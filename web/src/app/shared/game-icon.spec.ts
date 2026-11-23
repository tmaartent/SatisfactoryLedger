import { TestBed } from '@angular/core/testing';

import { CatalogNames } from './catalog-names';
import { GameIcon } from './game-icon';

describe("the game's icon beside a name", () => {
  async function render(inputs: { of?: string; itemNamed?: string; buildingNamed?: string; size?: number }) {
    TestBed.configureTestingModule({
      imports: [GameIcon],
      providers: [
        {
          provide: CatalogNames,
          useValue: {
            icon: (className: string) => (className === 'Desc_OreIron_C' ? '/api/v1/catalog/icons/Desc_OreIron_C' : null),
            iconOfItemNamed: (name: string) => (name === 'Iron Ore' ? '/api/v1/catalog/icons/Desc_OreIron_C' : null),
            iconOfBuildingNamed: (name: string) => (name === 'Miner Mk.3' ? '/api/v1/catalog/icons/Build_MinerMk3_C' : null),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(GameIcon);
    for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).querySelector('img');
  }

  it('shows the icon of an item, as decoration beside its name', async () => {
    const img = await render({ of: 'Desc_OreIron_C' });

    expect(img?.getAttribute('src')).toBe('/api/v1/catalog/icons/Desc_OreIron_C');
    expect(img?.getAttribute('alt')).toBe('');
    expect(img?.getAttribute('width')).toBe('20');
  });

  it('shows the icon of a building named as a trace names it', async () => {
    const img = await render({ buildingNamed: 'Miner Mk.3' });

    expect(img?.getAttribute('src')).toBe('/api/v1/catalog/icons/Build_MinerMk3_C');
  });

  it('shows the icon of an item named as a trace names it, at the size asked', async () => {
    const img = await render({ itemNamed: 'Iron Ore', size: 16 });

    expect(img?.getAttribute('src')).toBe('/api/v1/catalog/icons/Desc_OreIron_C');
    expect(img?.getAttribute('height')).toBe('16');
  });

  it('shows nothing for what has no icon', async () => {
    expect(await render({ of: 'Desc_Water_C' })).toBeNull();
  });
});
