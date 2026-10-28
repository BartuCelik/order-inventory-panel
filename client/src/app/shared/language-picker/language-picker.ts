import { Component, booleanAttribute, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import {
  LANGUAGES,
  Language,
  TranslatePipe,
  currentLanguage,
  setLanguage,
} from '../../core/i18n/i18n';

/** A button showing the current language; the menu lists each language in its own name. */
@Component({
  selector: 'app-language-picker',
  imports: [MatIconModule, MatMenuModule, TranslatePipe],
  template: `
    <button
      type="button"
      class="picker"
      [class.compact]="compact()"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="('language.label' | t) + ': ' + current.name"
    >
      <img class="lang-flag" [src]="flag(current.code)" alt="" />
      @if (compact()) {
        <span class="code">{{ current.code.toUpperCase() }}</span>
      } @else {
        <span>{{ current.name }}</span>
        <mat-icon class="chevron">expand_more</mat-icon>
      }
    </button>
    <mat-menu #menu="matMenu">
      @for (language of languages; track language.code) {
        <button
          mat-menu-item
          [attr.lang]="language.code"
          [attr.aria-current]="language.code === current.code"
          (click)="choose(language.code)"
        >
          <!-- The check sits inside the span: a direct mat-icon child would be moved to the start. -->
          <span class="item"
            ><img class="lang-flag" [src]="flag(language.code)" alt="" />{{ language.name }}
            @if (language.code === current.code) {
              <mat-icon class="check">check</mat-icon>
            }
          </span>
        </button>
      }
    </mat-menu>
  `,
  styles: `
    :host {
      display: block;
    }
    .picker {
      display: flex;
      align-items: center;
      gap: 10px;
      width: 100%;
      padding: 8px 10px;
      border: 0;
      border-radius: var(--radius-sm);
      background: var(--surface-muted);
      color: var(--text-2);
      font: inherit;
      font-size: 13.5px;
      font-weight: 550;
      cursor: pointer;
    }
    .picker:hover {
      color: var(--text);
    }
    /* In the top bar: flag and code only, a plain icon-sized button. */
    .picker.compact {
      gap: 8px;
      width: auto;
      height: 36px;
      padding: 0 10px;
      background: none;
    }
    .picker.compact:hover {
      background: var(--surface-muted);
    }
    .code {
      font-size: 13px;
    }
    mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .chevron {
      margin-left: auto;
    }
    /* Images, not emoji: Windows shows flag emoji as two letters. The name beside it is the label. */
    .lang-flag {
      width: 20px;
      height: 14px;
      flex: none;
      object-fit: cover;
      border-radius: 2px;
      box-shadow: 0 0 0 1px var(--border);
    }
    .item {
      display: inline-flex;
      align-items: center;
      gap: 10px;
    }
    .check {
      margin-left: 12px;
      color: var(--accent);
    }
  `,
})
export class LanguagePicker {
  /** Flag and language code only, for the top bar. */
  readonly compact = input(false, { transform: booleanAttribute });

  protected readonly languages = LANGUAGES;
  protected readonly current = LANGUAGES.find((l) => l.code === currentLanguage()) ?? LANGUAGES[0];

  /** English shows the UK flag (owner decision 2026-10-28). Files live in client/public/flags. */
  protected flag(language: Language): string {
    return `flags/${language}.svg`;
  }

  protected choose(language: Language): void {
    if (language !== this.current.code) {
      setLanguage(language);
    }
  }
}
