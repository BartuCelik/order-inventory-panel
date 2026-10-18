import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { HealthReport, HealthService, HealthStatus } from '../../core/health/health.service';

@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly healthService = inject(HealthService);

  protected readonly loading = signal(false);
  protected readonly report = signal<HealthReport | null>(null);
  protected readonly apiReachable = signal<boolean | null>(null);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.healthService.check().subscribe((report) => {
      this.report.set(report);
      this.apiReachable.set(report !== null);
      this.loading.set(false);
    });
  }

  protected statusIcon(status: HealthStatus): string {
    return status === 'Healthy' ? 'check_circle' : status === 'Degraded' ? 'warning' : 'error';
  }
}
