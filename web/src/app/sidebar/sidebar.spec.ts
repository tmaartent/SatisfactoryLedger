import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Sidebar } from './sidebar';
import { SidebarTab } from './sidebar-tab';
import { SidebarTabs } from './sidebar-tabs';

/** A page with two tabs, its content bound to the page's own state, and the sidebar beside it. */
@Component({
  imports: [Sidebar, SidebarTab],
  template: `
    @if (shown()) {
      <ng-template appSidebarTab="info" label="Info" icon="info"
        ><p class="content">{{ text() }}</p></ng-template
      >
    }
    <ng-template appSidebarTab="factories" label="Factories" icon="factory" [order]="1"
      ><p class="content">Rubber</p></ng-template
    >
    <app-sidebar />
  `,
})
class Page {
  readonly shown = signal(true);
  readonly text = signal('Constructor');
}

describe('the sidebar', () => {
  async function render() {
    const fixture = TestBed.createComponent(Page);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const icons = () => [...element.querySelectorAll<HTMLButtonElement>('.rail button')];
    const content = () => element.querySelector('app-sidebar .content')?.textContent;
    return { fixture, element, icons, content };
  }

  it("shows an icon for each of the page's tabs, and the first one open", async () => {
    const { icons, content } = await render();

    expect(icons().map((icon) => icon.getAttribute('aria-label'))).toEqual(['Info', 'Factories']);
    expect(content()).toBe('Constructor');
  });

  it("draws a tab with the page's own state, as it changes", async () => {
    const { fixture, content } = await render();

    fixture.componentInstance.text.set('Smelter');
    await fixture.whenStable();

    expect(content()).toBe('Smelter');
  });

  it('opens another tab when its icon is clicked', async () => {
    const { fixture, icons, content } = await render();

    icons()[1].click();
    await fixture.whenStable();

    expect(content()).toBe('Rubber');
  });

  it('collapses to the rail when the open tab is clicked again', async () => {
    const { fixture, element, icons } = await render();

    icons()[0].click();
    await fixture.whenStable();

    expect(element.querySelector('app-sidebar .tab')).toBeNull();
    expect(icons()).toHaveLength(2);
    expect(TestBed.inject(SidebarTabs).collapsed()).toBe(true);
  });

  it('loses a tab the page takes away', async () => {
    const { fixture, icons, content } = await render();

    fixture.componentInstance.shown.set(false);
    await fixture.whenStable();

    expect(icons().map((icon) => icon.getAttribute('aria-label'))).toEqual(['Factories']);
    expect(content()).toBe('Rubber');
  });
});
