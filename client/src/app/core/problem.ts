import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';
import { t } from './i18n/i18n';

/** RFC 7807 body the API returns for every error. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  /** Field errors on a 400 (ValidationProblemDetails), keyed by the API's property name, e.g. "MinStock". */
  errors?: Record<string, string[]>;
}

/** A message fit to show the user: the API's ProblemDetails detail when there is one. */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    if (problem?.detail) {
      return problem.detail;
    }
    if (error.status === 0) {
      return t('error.unreachable');
    }
    if (problem?.errors) {
      return Object.values(problem.errors).flat()[0] ?? t('error.checkFields');
    }
  }
  return t('error.generic');
}

/**
 * Puts the API's 400 field errors on the matching form controls (as a `server` error) and returns
 * true when at least one control took an error. Keys match case-insensitively ("MinStock" → minStock).
 */
export function applyServerErrors(error: unknown, form: FormGroup): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return false;
  }
  const errors = (error.error as ProblemDetails | null)?.errors ?? {};
  let applied = false;
  for (const [key, messages] of Object.entries(errors)) {
    const control = findControl(form, key);
    if (control && messages.length > 0) {
      control.setErrors({ server: messages[0] });
      control.markAsTouched();
      applied = true;
    }
  }
  return applied;
}

function findControl(form: FormGroup, key: string): AbstractControl | undefined {
  // JSON errors come as "$.minStock"; model errors as "MinStock".
  const name = key.replace(/^\$\./, '').toLowerCase();
  return Object.entries(form.controls).find(
    ([controlName]) => controlName.toLowerCase() === name,
  )?.[1];
}
