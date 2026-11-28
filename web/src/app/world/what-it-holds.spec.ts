import { TestBed } from '@angular/core/testing';

import { WhatItHolds } from './what-it-holds';

describe('what a snapshot holds', () => {
  it('counts its machines, logistics, providers, consumers and connections, saying what each is', async () => {
    TestBed.configureTestingModule({ imports: [WhatItHolds] });
    const fixture = TestBed.createComponent(WhatItHolds);
    fixture.componentRef.setInput('counts', {
      machines: 2425,
      logistics: 38699,
      providers: 416,
      consumers: 693,
      connections: 39228,
    });
    await fixture.whenStable();

    const element: HTMLElement = fixture.nativeElement;
    const rows = [...element.querySelectorAll('li')].map((row) =>
      (row.textContent ?? '').replace(/\s+/g, ' ').trim(),
    );
    expect(rows).toEqual([
      '2425 machines',
      '38699 belts, pipes, splitters and mergers',
      '416 providers: miners, extractors and stations that bring items in',
      '693 consumers: sinks, generators and stations that load',
      '39228 connections',
    ]);
  });
});
