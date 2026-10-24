import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { HEALTH_URL } from '../api';

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

export interface HealthCheckEntry {
  name: string;
  status: HealthStatus;
  durationMs: number;
  error: string | null;
}

export interface HealthReport {
  status: HealthStatus;
  totalDurationMs: number;
  checks: HealthCheckEntry[];
}

@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  /**
   * Calls the API's /health endpoint. When a dependency is down the API returns 503
   * but still includes the report in the body. Returns null if the API is unreachable.
   */
  check(): Observable<HealthReport | null> {
    return this.http.get<HealthReport>(HEALTH_URL).pipe(
      catchError((error: HttpErrorResponse) => of(isHealthReport(error.error) ? error.error : null)),
    );
  }
}

function isHealthReport(value: unknown): value is HealthReport {
  return typeof value === 'object' && value !== null && 'status' in value && 'checks' in value;
}
