import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api';
import { AuthService } from './auth.service';

/**
 * Sends the token with every API call, and signs out when the API answers 401 (token expired or revoked),
 * so the user lands on the login page instead of a screen of errors. Only our API gets the token.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!request.url.startsWith(API_BASE_URL)) {
    return next(request);
  }

  const token = auth.token();
  const authorized = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      // A failed sign-in is also a 401; the login page shows that one itself.
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !request.url.endsWith('/auth/login')
      ) {
        auth.logout(router.url);
      }
      return throwError(() => error);
    }),
  );
};
