import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type ThemeChoice = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'workshop.theme';
const ORDER: ThemeChoice[] = ['system', 'light', 'dark'];

/**
 * Light, dark or follow the system. The choice is a data-theme attribute on <html>; styles.scss
 * swaps every colour token on it, so no component knows about themes.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly choice = signal<ThemeChoice>(readStoredChoice());

  constructor() {
    const root = inject(DOCUMENT).documentElement;
    effect(() => {
      const choice = this.choice();
      if (choice === 'system') {
        root.removeAttribute('data-theme');
      } else {
        root.setAttribute('data-theme', choice);
      }
      try {
        localStorage.setItem(STORAGE_KEY, choice);
      } catch {
        // Storage blocked: the theme still applies for this visit.
      }
    });
  }

  /** System → light → dark → system. */
  cycle(): void {
    this.choice.update((current) => ORDER[(ORDER.indexOf(current) + 1) % ORDER.length]);
  }
}

function readStoredChoice(): ThemeChoice {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return ORDER.includes(stored as ThemeChoice) ? (stored as ThemeChoice) : 'system';
  } catch {
    return 'system';
  }
}
