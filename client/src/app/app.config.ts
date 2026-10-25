import {
  ApplicationConfig,
  Injectable,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { currentLanguage, t } from './core/i18n/i18n';

/** Material's paginator texts in the current language. */
@Injectable()
class TranslatedPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = t('paginator.itemsPerPage');
  override nextPageLabel = t('paginator.nextPage');
  override previousPageLabel = t('paginator.previousPage');
  override firstPageLabel = t('paginator.firstPage');
  override lastPageLabel = t('paginator.lastPage');
  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0 || pageSize === 0) {
      return t('paginator.empty', { total: length });
    }
    const start = page * pageSize + 1;
    return t('paginator.range', {
      start,
      end: Math.min(start + pageSize - 1, length),
      total: length,
    });
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    // Dates and numbers (DatePipe, DecimalPipe) follow the chosen language.
    { provide: LOCALE_ID, useFactory: currentLanguage },
    { provide: MatPaginatorIntl, useClass: TranslatedPaginatorIntl },
  ],
};
