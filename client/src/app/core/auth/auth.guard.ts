import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Keeps the app behind the login page; the requested URL comes back as `returnUrl`. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isSignedIn() ||
  inject(Router).createUrlTree(['/login'], {
    queryParams: state.url !== '/' ? { returnUrl: state.url } : {},
  });
