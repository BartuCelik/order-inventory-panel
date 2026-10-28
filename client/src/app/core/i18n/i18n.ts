import { registerLocaleData } from '@angular/common';
import { Pipe, PipeTransform } from '@angular/core';
import { en } from './messages/en';

/**
 * UI translations. English (messages/en.ts) is the source: its keys are the only valid keys, and every
 * other language is typed against it, so a missing or misspelt key fails the build.
 *
 * The language is fixed for the life of the page: main.ts loads it before any app code runs, and
 * switching reloads the page. That keeps it simple — module-level labels, the Angular LOCALE_ID
 * (dates, numbers) and Material's texts are all set once, in the right language.
 */

export type MessageKey = keyof typeof en;

/** Plural forms by Intl.PluralRules category; `other` is required, the rest as the language needs them. */
export type PluralForms = Partial<Record<Intl.LDMLPluralRule, string>> & { other: string };

export type Messages = {
  [K in MessageKey]: (typeof en)[K] extends string ? string : PluralForms;
};

export const LANGUAGES = [
  { code: 'en', name: 'English' },
  { code: 'tr', name: 'Türkçe' },
  { code: 'de', name: 'Deutsch' },
  { code: 'fr', name: 'Français' },
  { code: 'es', name: 'Español' },
  { code: 'it', name: 'Italiano' },
] as const;

export type Language = (typeof LANGUAGES)[number]['code'];

const DEFAULT_LANGUAGE: Language = 'en';
const STORAGE_KEY = 'workshop.language';

// Each language is its own chunk: a visitor downloads only the one they use.
const LOADERS: Record<Language, () => Promise<[Messages, unknown]>> = {
  en: async () => [en, undefined],
  tr: () =>
    Promise.all([
      import('./messages/tr').then((m) => m.tr),
      locale(import('@angular/common/locales/tr')),
    ]),
  de: () =>
    Promise.all([
      import('./messages/de').then((m) => m.de),
      locale(import('@angular/common/locales/de')),
    ]),
  fr: () =>
    Promise.all([
      import('./messages/fr').then((m) => m.fr),
      locale(import('@angular/common/locales/fr')),
    ]),
  es: () =>
    Promise.all([
      import('./messages/es').then((m) => m.es),
      locale(import('@angular/common/locales/es')),
    ]),
  it: () =>
    Promise.all([
      import('./messages/it').then((m) => m.it),
      locale(import('@angular/common/locales/it')),
    ]),
};

function locale(module: Promise<{ default: unknown }>): Promise<unknown> {
  return module.then((m) => m.default);
}

let active: { language: Language; messages: Messages; plural: Intl.PluralRules } | null = null;

/** Loads the saved language (English by default). main.ts awaits this before importing the app. */
export async function loadLanguage(): Promise<void> {
  const language = savedLanguage();
  const [messages, localeData] = await LOADERS[language]();
  if (localeData) {
    registerLocaleData(localeData, language);
  }
  active = { language, messages, plural: new Intl.PluralRules(language) };
  document.documentElement.lang = language;
}

export function currentLanguage(): Language {
  return active?.language ?? DEFAULT_LANGUAGE;
}

/** Saves the choice and reloads, so every text, date and number is rendered in the new language. */
export function setLanguage(language: Language): void {
  try {
    localStorage.setItem(STORAGE_KEY, language);
  } catch {
    // Storage blocked: nothing to remember the choice with, so the reload would undo it.
    return;
  }
  location.reload();
}

/**
 * The text for a key. `{name}` placeholders take values from `params`; a plural entry picks its form
 * from `params.count`.
 */
export function t(key: MessageKey, params?: Record<string, string | number>): string {
  if (!active) {
    throw new Error(`Translation "${key}" requested before loadLanguage() finished.`);
  }
  const entry = active.messages[key] as string | PluralForms;
  const text =
    typeof entry === 'string'
      ? entry
      : (entry[active.plural.select(Number(params?.['count'] ?? 0))] ?? entry.other);
  return params
    ? text.replace(/\{(\w+)\}/g, (match, name: string) => String(params[name] ?? match))
    : text;
}

/** A browser tab title: "Work orders · Workshop". */
export function pageTitle(key: MessageKey): string {
  return `${t(key)} · ${t('app.titleSuffix')}`;
}

/** `{{ 'nav.overview' | t }}`, `{{ 'dashboard.ofOpen' | t: { count: n } }}` */
@Pipe({ name: 't' })
export class TranslatePipe implements PipeTransform {
  transform(key: MessageKey, params?: Record<string, string | number>): string {
    return t(key, params);
  }
}

function savedLanguage(): Language {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    return LANGUAGES.some((l) => l.code === saved) ? (saved as Language) : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
}
