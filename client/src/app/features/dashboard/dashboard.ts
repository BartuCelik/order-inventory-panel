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
import { stockLevel, unitLabel } from '../products/product.models';
import { movementTypeChip, movementTypeLabel } from '../stock/stock.models';
import { DashboardSummary } from './dashboard.models';

@Component({
  selector: 'app-dashboard',
  imports: [
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
  protected readonly unitLabel = unitLabel;
  protected readonly stockLevel = stockLevel;
  protected readonly movementTypeLabel = movementTypeLabel;
  protected readonly movementTypeChip = movementTypeChip;
  protected readonly errorMessage = errorMessage;

  protected readonly summary = httpResource<DashboardSummary>(() => `${API_BASE_URL}/dashboard`);

  protected readonly activeProducts = computed(() => {
    if (!this.summary.hasValue()) {
      return 0;
    }
    const { rawMaterial, semiFinished, finished } = this.summary.value().products;
    return rawMaterial + semiFinished + finished;
  });

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
  /** The busiest day sets the chart's scale; at least 1 so an empty fortnight still draws. */
  private readonly chartMax = computed(() =>
    Math.max(1, ...this.activity().map((d) => Math.max(d.in, d.out))),
  );

  protected barHeight(count: number): number {
    return (count / this.chartMax()) * 100;
  }

  // ---------- system status ----------

  protected readonly healthLoading = signal(false);
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
    this.healthLoading.set(true);
    this.healthService.check().subscribe((report) => {
      this.report.set(report);
      this.apiReachable.set(report !== null);
      this.healthLoading.set(false);
    });
  }

  protected statusIcon(status: HealthStatus): string {
    return status === 'Healthy' ? 'check_circle' : status === 'Degraded' ? 'warning' : 'error';
  }
}
