import { HttpInterceptorFn } from '@angular/common/http';

import { environment } from '../../environments/environment';

const ABSOLUTE_URL: RegExp = /^https?:\/\//i;

/** Prefix relative request URLs with `environment.apiUrl`; absolute URLs pass through untouched. */
export const baseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  if(ABSOLUTE_URL.test(req.url)) {
    return next(req);
  }
  const url: string = `${environment.apiUrl}/${req.url.replace(/^\//, '')}`;
  return next(req.clone({ url }));
};
