import { TemplateRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { SidebarTabs, Tab } from './sidebar-tabs';

const tab = (key: string, order = 0): Tab => ({
  key,
  label: key,
  icon: key,
  order,
  template: {} as TemplateRef<unknown>,
});

describe('the tabs of the sidebar', () => {
  const tabs = () => TestBed.inject(SidebarTabs);

  it('starts open, on the first tab the page gives', () => {
    tabs().add(tab('factories'));
    tabs().add(tab('snapshots', 1));

    expect(tabs().collapsed()).toBe(false);
    expect(tabs().current()?.key).toBe('factories');
  });

  it('keeps the explanation last, whichever page adds its tabs after it', () => {
    tabs().add(tab('explanation', 9));
    tabs().add(tab('info'));
    tabs().add(tab('factories', 1));

    expect(
      tabs()
        .tabs()
        .map((t) => t.key),
    ).toEqual(['info', 'factories', 'explanation']);
  });

  it('shows a tab asked for, and opens the sidebar for it when it was collapsed', () => {
    tabs().add(tab('info'));
    tabs().add(tab('explanation', 9));
    tabs().collapsed.set(true);

    tabs().show('explanation');

    expect(tabs().current()?.key).toBe('explanation');
    expect(tabs().collapsed()).toBe(false);
  });

  it("collapses when the open tab's icon is clicked, and opens again on the next click", () => {
    tabs().add(tab('info'));

    tabs().toggle('info');
    expect(tabs().collapsed()).toBe(true);

    tabs().toggle('info');
    expect(tabs().collapsed()).toBe(false);
  });

  it('remembers the tab shown on a page that left, and shows it again when a page brings it back', () => {
    const info = tab('info');
    tabs().add(info);
    tabs().add(tab('explanation', 9));
    tabs().show('info');

    tabs().remove(info);
    tabs().add(tab('factories', 1));
    expect(tabs().current()?.key).toBe('factories');

    tabs().add(tab('info'));
    expect(tabs().current()?.key).toBe('info');
  });

  describe('before a 3D view', () => {
    const screen = (phone: boolean) =>
      vi.stubGlobal(
        'matchMedia',
        vi.fn((query: string) => ({ matches: phone && query === '(max-width: 599px)' })),
      );
    afterEach(() => vi.unstubAllGlobals());

    it('closes on a phone, where its sheet would lie over the view', () => {
      screen(true);

      tabs().closeOnPhone();

      expect(tabs().collapsed()).toBe(true);
    });

    it('stays open on a wider screen, where it stands beside the view', () => {
      screen(false);

      tabs().closeOnPhone();

      expect(tabs().collapsed()).toBe(false);
    });
  });
});
