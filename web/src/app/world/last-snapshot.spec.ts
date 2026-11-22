import { TestBed } from '@angular/core/testing';

import { LastSnapshot } from './last-snapshot';

describe('the last snapshot looked at', () => {
  beforeEach(() => localStorage.clear());

  it('is none at first', () => {
    expect(TestBed.inject(LastSnapshot).id()).toBeNull();
  });

  it('is remembered when the site is opened again', () => {
    TestBed.inject(LastSnapshot).remember('s1');
    TestBed.resetTestingModule();

    expect(TestBed.inject(LastSnapshot).id()).toBe('s1');
  });

  it('is forgotten when it is deleted', () => {
    const last = TestBed.inject(LastSnapshot);
    last.remember('s1');

    last.forget('s1');

    expect(last.id()).toBeNull();
    TestBed.resetTestingModule();
    expect(TestBed.inject(LastSnapshot).id()).toBeNull();
  });

  it('stays when another snapshot is deleted', () => {
    const last = TestBed.inject(LastSnapshot);
    last.remember('s1');

    last.forget('s2');

    expect(last.id()).toBe('s1');
  });
});
