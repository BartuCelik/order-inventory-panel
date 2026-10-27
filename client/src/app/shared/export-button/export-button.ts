import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { saveFile } from '../../core/download';
import { TranslatePipe, t } from '../../core/i18n/i18n';
import { errorMessage } from '../../core/problem';

/**
 * Downloads `{url}/export` as an Excel file. Pass the list's filters without paging: the API
 * exports every row they match.
 */
@Component({
  selector: 'app-export-button',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <button mat-stroked-button type="button" (click)="export()" [disabled]="busy()">
      <mat-icon>download</mat-icon>
      {{ 'common.exportExcel' | t }}
    </button>
  `,
})
export class ExportButton {
  private readonly http = inject(HttpClient);
  private readonly snackBar = inject(MatSnackBar);

  /** The list endpoint, e.g. `${API_BASE_URL}/products`. */
  readonly url = input.required<string>();
  readonly filters = input<Record<string, string | number | boolean>>({});
  /** File name without date and extension, e.g. `products`. */
  readonly fileName = input.required<string>();

  protected readonly busy = signal(false);

  protected export(): void {
    this.busy.set(true);
    this.http
      .get(`${this.url()}/export`, { params: this.filters(), responseType: 'blob' })
      .subscribe({
        next: (blob) => {
          this.busy.set(false);
          saveFile(blob, `${this.fileName()}-${new Date().toISOString().slice(0, 10)}.xlsx`);
        },
        error: async (error: unknown) => {
          this.busy.set(false);
          this.snackBar.open(errorMessage(await problemFromBlob(error)), t('common.close'));
        },
      });
  }
}

/** With responseType 'blob' an error body arrives as a Blob too; parse it so the server's detail is shown. */
async function problemFromBlob(error: unknown): Promise<unknown> {
  if (!(error instanceof HttpErrorResponse) || !(error.error instanceof Blob)) {
    return error;
  }
  try {
    return new HttpErrorResponse({
      error: JSON.parse(await error.error.text()),
      status: error.status,
    });
  } catch {
    return error;
  }
}
