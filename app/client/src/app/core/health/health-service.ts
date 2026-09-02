import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { SKIP_ERROR_MAPPING } from '../http/skip-error-mapping';
import { HealthReport } from '../types/health-report';

@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly healthUrl: string = environment.healthUrl;

  check(): Observable<HealthReport> {
    return this.http
      .get<HealthReport>(this.healthUrl, {
        context: new HttpContext().set(SKIP_ERROR_MAPPING, true),
      })
    .pipe(catchError((error: HttpErrorResponse) => of(this.toReport(error))));
  }

  private toReport(error: HttpErrorResponse): HealthReport {
    const body: unknown = error.error;
    const looksLikeReport: boolean =
      body !== null &&
      typeof body === 'object' &&
      typeof (body as { status?: unknown }).status === 'string';
    return looksLikeReport ? (body as HealthReport) : { status: 'Unhealthy', totalDurationMs: 0, checks: [] };
  }
}
