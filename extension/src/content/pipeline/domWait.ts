import { hasExtractionSignals } from './visibleTextExtractor';

export interface DomWaitOptions {
  timeoutMs?: number;
  idleMs?: number;
}

export function waitForExtractionSignals(
  documentRef: Document,
  options: DomWaitOptions = {}
): Promise<void> {
  const timeoutMs = options.timeoutMs ?? 2500;
  const idleMs = options.idleMs ?? 200;

  if (hasExtractionSignals(documentRef)) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    let idleTimer: ReturnType<typeof setTimeout> | undefined;
    let timeoutTimer: ReturnType<typeof setTimeout> | undefined;
    let observer: MutationObserver | undefined;

    const finish = () => {
      if (idleTimer) {
        clearTimeout(idleTimer);
      }

      if (timeoutTimer) {
        clearTimeout(timeoutTimer);
      }

      observer?.disconnect();
      resolve();
    };

    const scheduleIdleFinish = () => {
      if (idleTimer) {
        clearTimeout(idleTimer);
      }

      idleTimer = setTimeout(finish, idleMs);
    };

    observer = new MutationObserver(() => {
      if (hasExtractionSignals(documentRef)) {
        scheduleIdleFinish();
      }
    });

    observer.observe(documentRef.documentElement, {
      childList: true,
      subtree: true,
      characterData: true
    });

    timeoutTimer = setTimeout(finish, timeoutMs);
  });
}
