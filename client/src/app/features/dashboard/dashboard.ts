import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { API_BASE_URL } from '../../core/api';
import { HealthReport, HealthService, HealthStatus } from '../../core/health/health.service';
import { errorMessage } from '../../core/problem';
import { daysUntil, dueLabel } from '../orders/order.models';
import { stockLevel, unitLabel } from '../products/product.models';
import { statusClass, statusLabel } from '../work-orders/work-order.models';
import { DashboardSummary } from './dashboard.models';
import { ProductionChart } from './production-chart/production-chart';
import { AuthService } from '../../core/auth/auth.service';
import { t, TranslatePipe } from '../../core/i18n/i18n';

@Component({
  selector: 'app-dashboard',
  imports: [
    TranslatePipe,
    ProductionChart,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly healthService = inject(HealthService);

  protected readonly today = new Date();
  private readonly auth = inject(AuthService);

  /** "Good morning, Mira": the large title greets whoever is signed in. */
  protected readonly greeting = computed(() => {
    const hour = this.today.getHours();
    const greeting = t(
      hour < 12 ? 'greeting.morning' : hour < 18 ? 'greeting.afternoon' : 'greeting.evening',
    );
    const name = this.auth.user()?.displayName.split(' ')[0];
    return name ? t('greeting.named', { greeting, name }) : greeting;
  });
  protected readonly unitLabel = unitLabel;
  protected readonly stockLevel = stockLevel;
  protected readonly statusClass = statusClass;
  protected readonly statusLabel = statusLabel;
  protected readonly dueLabel = dueLabel;
  protected readonly errorMessage = errorMessage;

  protected healthLabel(status: HealthStatus): string {
    return t(`health.${status}`);
  }

  protected readonly summary = httpResource<DashboardSummary>(() => `${API_BASE_URL}/dashboard`);

  protected readonly activity = computed(() =>
    this.summary.hasValue() ? this.summary.value().activity : [],
  );
  protected readonly todayActivity = computed(() => this.activity().at(-1) ?? { in: 0, out: 0 });
  protected readonly periodTotals = computed(() =>
    this.activity().reduce((sum, d) => ({ in: sum.in + d.in, out: sum.out + d.out }), {
      in: 0,
      out: 0,
    }),
  );
  /** The busiest day, rounded up to an even number, sets the chart's scale (at least 2). */
  protected readonly chartMax = computed(() => {
    const max = Math.max(2, ...this.activity().map((d) => Math.max(d.in, d.out)));
    return Math.ceil(max / 2) * 2;
  });
  protected readonly chartLabel = computed(() =>
    t('dashboard.chartLabel', { in: this.periodTotals().in, out: this.periodTotals().out }),
  );

  protected barHeight(count: number): number {
    return (count / this.chartMax()) * 100;
  }

  /** "Today", "Tomorrow", "In 3 days", "2 days overdue" for a work order's planned date. */
  protected planLabel(date: string): string {
    const days = daysUntil(date);
    if (days === 0) return t('plan.today');
    if (days === 1) return t('plan.tomorrow');
    if (days > 1) return t('plan.inDays', { count: days });
    return days === -1 ? t('plan.yesterday') : t('plan.daysAgo', { count: -days });
  }

  // ---------- system status ----------

  protected readonly report = signal<HealthReport | null>(null);
  protected readonly apiReachable = signal<boolean | null>(null);

  constructor() {
    this.checkHealth();
  }

  protected refresh(): void {
    this.summary.reload();
    this.checkHealth();
  }

  private checkHealth(): void {
    this.healthService.check().subscribe((report) => {
      this.report.set(report);
      this.apiReachable.set(report !== null);
    });
  }
}
