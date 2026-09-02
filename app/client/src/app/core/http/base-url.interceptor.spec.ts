import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { baseUrlInterceptor } from './base-url.interceptor';

describe('baseUrlInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('prefixes a relative URL with the API base', () => {
    const expected: string = `${environment.apiUrl}/expenses`;
    http.get('expenses').subscribe();
    const req = httpMock.expectOne(expected);
    expect(req.request.url).toBe(expected);
    req.flush({});
  });
  it('normalizes a leading slash on the relative URL', () => {
    const expected: string = `${environment.apiUrl}/expenses`;
    http.get('/expenses').subscribe();
    const req = httpMock.expectOne(expected);
    expect(req.request.url).toBe(expected);
    req.flush({});
  });
  it('leaves an absolute http(s) URL untouched', () => {
    const absolute: string = 'https://localhost:7095/health';
    http.get(absolute).subscribe();
    const req = httpMock.expectOne(absolute);
    expect(req.request.url).toBe(absolute);
    req.flush({});
  });
});
