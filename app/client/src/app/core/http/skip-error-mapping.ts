import { HttpContextToken } from '@angular/common/http';

/** Per-request opt-out: when true, `problemDetailsInterceptor` leaves the error untouched. */
export const SKIP_ERROR_MAPPING: HttpContextToken<boolean> = new HttpContextToken<boolean>(
  () => false,
);
