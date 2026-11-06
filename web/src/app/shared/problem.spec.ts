import { HttpErrorResponse } from '@angular/common/http';

import { problemMessage, whenAnalysed } from './problem';

describe('whenAnalysed', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('asks again while the analysis is pending, and gives the findings once it is ready', async () => {
    const answers: { status: number; body: unknown }[] = [
      { status: 202, body: { status: 'pending' } },
      { status: 200, body: { settled: true } },
    ];
    const call = vi.fn(() => Promise.resolve(answers.shift()!));

    const findings = whenAnalysed(call, new AbortController().signal);
    await vi.advanceTimersByTimeAsync(2000);

    expect(await findings).toEqual({ settled: true });
    expect(call).toHaveBeenCalledTimes(2);
  });

  it('stops asking when the page is left', async () => {
    const leaving = new AbortController();
    const call = vi.fn(() => Promise.resolve({ status: 202, body: {} }));

    const findings = whenAnalysed(call, leaving.signal);
    await vi.advanceTimersByTimeAsync(0);
    leaving.abort();

    await expect(findings).rejects.toBeDefined();
    expect(call).toHaveBeenCalledTimes(1);
  });
});

describe('problemMessage', () => {
  it('tells the reason the API gave', () => {
    const error = new HttpErrorResponse({ status: 500, error: { title: 'Analysis failed', detail: 'The service ran out of patience' } });

    expect(problemMessage(error)).toBe('The service ran out of patience');
  });
});
