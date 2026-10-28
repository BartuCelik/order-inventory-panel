import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, roleLabel } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme.service';
import { t, TranslatePipe } from '../../core/i18n/i18n';
import { LanguagePicker } from '../../shared/language-picker/language-picker';

interface NavItem {
  label: string;
  route: string;
}

interface NavGroup {
  /** Stable id for aria-labelledby; the label is translated. */
  id: string;
  label: string;
  items: NavItem[];
}

@Component({
  selector: 'app-shell',
  imports: [
    TranslatePipe,
    LanguagePicker,
    DatePipe,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly roleLabel = roleLabel;

  protected readonly themeLabel = computed(
    () =>
      ({ system: t('theme.system'), light: t('theme.light'), dark: t('theme.dark') })[
        this.theme.choice()
      ],
  );
  protected readonly themeIcon = computed(
    () => ({ system: 'contrast', light: 'light_mode', dark: 'dark_mode' })[this.theme.choice()],
  );

  protected initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join('');
  }

  /** Grouped the way the workshop works: what is being made, what was ordered, what is on the shelves. */
  protected readonly navGroups: NavGroup[] = [
    {
      id: 'floor',
      label: t('nav.floor'),
      items: [
        { label: t('nav.overview'), route: '/dashboard' },
        { label: t('nav.workOrders'), route: '/work-orders' },
      ],
    },
    {
      id: 'orders',
      label: t('nav.orders'),
      items: [
        { label: t('nav.salesOrders'), route: '/sales-orders' },
        { label: t('nav.customers'), route: '/customers' },
      ],
    },
    {
      id: 'inventory',
      label: t('nav.inventory'),
      items: [
        { label: t('nav.products'), route: '/products' },
        { label: t('nav.stockLedger'), route: '/stock' },
      ],
    },
  ];

  /** Wall clock in the footer, refreshed every 20 seconds. */
  protected readonly now = signal(new Date());

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 20_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
}
