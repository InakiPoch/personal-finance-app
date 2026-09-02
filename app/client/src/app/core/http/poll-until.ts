import { Observable, first, retry, switchMap, take, timer } from 'rxjs';

export type PollUntilOptions = {
  intervalMs?: number;
  maxAttempts?: number;
};

/**
 * Poll `poll()` starting immediately and then every `intervalMs` (default 800), up to `maxAttempts` (default 5).
 */
export function pollUntil<T>(poll: () => Observable<T>, done: (value: T) => boolean, options: PollUntilOptions = {}): Observable<T> {
  const intervalMs: number = options.intervalMs ?? 800;
  const maxAttempts: number = options.maxAttempts ?? 5;
  return timer(0, intervalMs).pipe(
    take(maxAttempts),
    switchMap(() => poll().pipe(retry(1))),
    first(done),
  );
}
