import { Component } from '@angular/core';
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
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="('language.label' | t) + ': ' + current.name"
    >
      <mat-icon>translate</mat-icon>
      <span>{{ current.name }}</span>
      <mat-icon class="chevron">expand_more</mat-icon>
    </button>
    <mat-menu #menu="matMenu">
      @for (language of languages; track language.code) {
        <button
          mat-menu-item
          [attr.lang]="language.code"
          [attr.aria-current]="language.code === current.code"
          (click)="choose(language.code)"
        >
          <span>{{ language.name }}</span>
          @if (language.code === current.code) {
            <mat-icon class="check">check</mat-icon>
          }
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
    mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .chevron {
      margin-left: auto;
    }
    .check {
      margin-left: 12px;
      color: var(--accent);
    }
  `,
})
export class LanguagePicker {
  protected readonly languages = LANGUAGES;
  protected readonly current = LANGUAGES.find((l) => l.code === currentLanguage()) ?? LANGUAGES[0];

  protected choose(language: Language): void {
    if (language !== this.current.code) {
      setLanguage(language);
    }
  }
}
