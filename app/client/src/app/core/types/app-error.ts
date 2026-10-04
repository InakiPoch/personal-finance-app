export type AppError = {
  code: string;
  title: string;
  detail: string;
  status: number;
  metadata: Record<string, unknown>;
};
