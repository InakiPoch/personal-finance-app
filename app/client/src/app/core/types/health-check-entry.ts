import { HealthStatus } from './health-status';

export type HealthCheckEntry = {
  name: string;
  status: HealthStatus;
  description: string;
  data: Record<string, unknown>;
};
