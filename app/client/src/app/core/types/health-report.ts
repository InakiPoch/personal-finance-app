import { HealthCheckEntry } from './health-check-entry';
import { HealthStatus } from '../health/health-status';

export type HealthReport = {
  status: HealthStatus;
  totalDurationMs: number;
  checks: HealthCheckEntry[];
};
