import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { API_BASE_URL } from '../api';

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
   * API'nin /health endpoint'ini çağırır. Bir bağımlılık çalışmadığında API 503 döner
   * ama gövdede yine rapor bulunur. API'ye hiç ulaşılamıyorsa null döner.
   */
  check(): Observable<HealthReport | null> {
    return this.http.get<HealthReport>(`${API_BASE_URL}/health`).pipe(
      catchError((error: HttpErrorResponse) => of(isHealthReport(error.error) ? error.error : null)),
    );
  }
}

function isHealthReport(value: unknown): value is HealthReport {
  return typeof value === 'object' && value !== null && 'status' in value && 'checks' in value;
}
