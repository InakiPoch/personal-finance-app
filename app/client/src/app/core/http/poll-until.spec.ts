import { defer, EmptyError, of, throwError } from 'rxjs';
import { TestScheduler } from 'rxjs/testing';

import { pollUntil } from './poll-until';

describe('pollUntil', () => {
  let scheduler: TestScheduler;

  beforeEach(() => {
    scheduler = new TestScheduler((actual, expected) => expect(actual).toEqual(expected));
  });

  it('emits the first value where done is true, then completes', () => {
    scheduler.run(({ expectObservable }) => {
      let attempt: number = 0;
      const source = pollUntil(
        () => of(++attempt),
        (value: number) => value >= 3,
        { intervalMs: 10, maxAttempts: 5 },
      );
      expectObservable(source).toBe('20ms (a|)', { a: 3 });
    });
  });
  it('errors with EmptyError when the attempts run out with no match', () => {
    scheduler.run(({ expectObservable }) => {
      const source = pollUntil(
        () => of(1),
        (value: number) => value >= 99,
        { intervalMs: 10, maxAttempts: 3 },
      );
      expectObservable(source).toBe('20ms #', undefined, jasmine.any(EmptyError));
    });
  });
  it('retries a failing attempt once via retry(1)', () => {
    scheduler.run(({ expectObservable }) => {
      let subscribes: number = 0;
      const source = pollUntil(
        () =>
          defer(() => {
            subscribes += 1;
            return subscribes === 1 ? throwError(() => new Error('transient')) : of(42);
          }),
        (value: number) => value === 42,
        { intervalMs: 10, maxAttempts: 3 },
      );
      expectObservable(source).toBe('(a|)', { a: 42 });
    });
  });
});
