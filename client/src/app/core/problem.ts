import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 body the API returns for every error. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
}

/** A message fit to show the user: the API's ProblemDetails detail when there is one. */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    if (problem?.detail) {
      return problem.detail;
    }
    if (error.status === 0) {
      return 'The API cannot be reached.';
    }
  }
  return 'Something went wrong. Please try again.';
}
