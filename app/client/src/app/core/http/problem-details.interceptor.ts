import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { AppError } from '../types/app-error';
import { SKIP_ERROR_MAPPING } from './skip-error-mapping';

/** Root ProblemDetails keys that map to named `AppError` fields rather than `metadata`. */
const RESERVED_KEYS: ReadonlySet<string> = new Set(['type', 'title', 'status', 'detail', 'code']);

/** Normalize every failed response to an `AppError`, unless the request opted out. */
export const problemDetailsInterceptor: HttpInterceptorFn = (req, next) => {
  if(req.context.get(SKIP_ERROR_MAPPING)) {
    return next(req);
  }
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => throwError(() => toAppError(error))),
  );
};

function toAppError(error: HttpErrorResponse): AppError {
  const body: unknown = error.error;
  const hasProblemBody: boolean =
    error.status !== 0 &&
    body !== null &&
    typeof body === 'object' &&
    !(body instanceof ProgressEvent) &&
    !(body instanceof Error);
  if(hasProblemBody) {
    return fromProblemDetails(body as Record<string, unknown>, error.status);
  }
  return fromTransport(error);
}

function fromProblemDetails(body: Record<string, unknown>, httpStatus: number): AppError {
  const rawTitle: string | undefined = typeof body['title'] === 'string' ? body['title'] : undefined;
  const rawDetail: string | undefined = typeof body['detail'] === 'string' ? body['detail'] : undefined;
  const status: number = typeof body['status'] === 'number' ? body['status'] : httpStatus;
  const code: string = typeof body['code'] === 'string' ? body['code'] : deriveCode(status);
  const metadata: Record<string, unknown> = {};
  for(const key of Object.keys(body)) {
    if(!RESERVED_KEYS.has(key)) {
      metadata[key] = body[key];
    }
  }
  return {
    code,
    title: rawTitle ?? defaultTitle(status),
    detail: rawDetail ?? rawTitle ?? defaultTitle(status),
    status,
    metadata,
  };
}

function fromTransport(error: HttpErrorResponse): AppError {
  const status: number = error.status;
  const code: string =
    status === 0 ? 'Http.NetworkError' : status >= 500 ? 'Http.ServerError' : 'Http.RequestFailed';
  return {
    code,
    title: defaultTitle(status),
    detail: error.message,
    status,
    metadata: {},
  };
}

function deriveCode(status: number): string {
  switch(status) {
    case 400:
      return 'Http.BadRequest';
    case 404:
      return 'Http.NotFound';
    case 409:
      return 'Http.Conflict';
    case 422:
      return 'Http.UnprocessableEntity';
    case 0:
      return 'Http.NetworkError';
    default:
      return status >= 500 ? 'Http.ServerError' : 'Http.RequestFailed';
  }
}

function defaultTitle(status: number): string {
  switch(status) {
    case 400:
      return 'Bad request';
    case 404:
      return 'Not found';
    case 409:
      return 'Conflict';
    case 422:
      return 'Unprocessable entity';
    case 0:
      return 'Network error';
    default:
      return status >= 500 ? 'Server error' : 'Request failed';
  }
}
