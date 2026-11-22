import { DestroyRef, Directive, inject, input, OnInit, TemplateRef } from '@angular/core';

import { SidebarTabs } from './sidebar-tabs';

/**
 * Declares a tab of the sidebar where its content is written, in the page's own template:
 * `<ng-template appSidebarTab="info" label="Info" icon="info">…</ng-template>`. The tab is there while the page is, and
 * its content keeps the page's bindings and styles, wherever the sidebar draws it.
 */
@Directive({ selector: 'ng-template[appSidebarTab]' })
export class SidebarTab implements OnInit {
  private readonly tabs = inject(SidebarTabs);
  private readonly template = inject(TemplateRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly key = input.required<string>({ alias: 'appSidebarTab' });
  readonly label = input.required<string>();
  readonly icon = input.required<string>();
  readonly order = input(0);

  ngOnInit(): void {
    const tab = { key: this.key(), label: this.label(), icon: this.icon(), order: this.order(), template: this.template };
    this.tabs.add(tab);
    this.destroyRef.onDestroy(() => this.tabs.remove(tab));
  }
}
