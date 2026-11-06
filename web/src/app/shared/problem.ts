import { HttpErrorResponse } from '@angular/common/http';

/** The message of a failed call: the API's problem details (RFC 9457) when it sent them. */
export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
    return problem?.detail ?? (validation || problem?.title) ?? `${error.status} ${error.statusText}`;
  }
  return error instanceof Error ? error.message : String(error);
}

/** Waits, unless the page is left first. */
export function delay(milliseconds: number, abortSignal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, milliseconds);
    abortSignal.addEventListener('abort', () => {
      clearTimeout(timer);
      reject(abortSignal.reason);
    });
  });
}

/** Calls an analysis endpoint until it stops answering 202 Accepted: the analysis is pending (ADR-0017). */
export async function whenAnalysed<T>(call: () => Promise<{ status: number; body: T }>, abortSignal: AbortSignal): Promise<T> {
  for (;;) {
    const response = await call();
    if (response.status !== 202) return response.body;
    await delay(2000, abortSignal);
  }
}
