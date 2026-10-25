import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

interface NavGroup {
  label: string;
  items: NavItem[];
}

@Component({
  selector: 'app-shell',
  imports: [
    DatePipe,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);

  protected readonly themeLabel = computed(
    () => ({ system: 'Match system', light: 'Light', dark: 'Dark' })[this.theme.choice()],
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
      label: 'Floor',
      items: [
        { label: 'Overview', icon: 'space_dashboard', route: '/dashboard' },
        { label: 'Work orders', icon: 'precision_manufacturing', route: '/work-orders' },
      ],
    },
    {
      label: 'Orders',
      items: [
        { label: 'Sales orders', icon: 'receipt_long', route: '/sales-orders' },
        { label: 'Customers', icon: 'storefront', route: '/customers' },
      ],
    },
    {
      label: 'Inventory',
      items: [
        { label: 'Products', icon: 'category', route: '/products' },
        { label: 'Stock ledger', icon: 'inventory', route: '/stock' },
      ],
    },
  ];

  /** Below this width the rail becomes a drawer opened from a top bar. */
  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 900px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  /** Wall clock in the rail, refreshed every 20 seconds. */
  protected readonly now = signal(new Date());

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 20_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
}
