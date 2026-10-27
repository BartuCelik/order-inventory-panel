import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable, Subscription, filter } from 'rxjs';
import { TranslatePipe, t } from '../../core/i18n/i18n';
import { ProblemDetails, errorMessage } from '../../core/problem';

export interface ImportDialogData {
  title: string;
  hint: string;
  /** The import endpoint, e.g. `${API_BASE_URL}/products/import`. */
  url: string;
  /** Labels for the counts; a count without a label is not shown. */
  labels: { created: string; updated?: string; unchanged: string };
}

/** What the API wrote, or with dryRun would write. */
export interface ImportResult {
  created: number;
  updated: number;
  unchanged: number;
  dryRun: boolean;
}

interface Problem {
  where: string;
  messages: string[];
}

/**
 * Upload → preview (the API's dry run: counts, or every problem by row) → Import. The API checks the
 * whole file and writes all of it or nothing, so the preview is exactly what Import will do.
 */
@Component({
  selector: 'app-import-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule, TranslatePipe],
  templateUrl: './import-dialog.html',
  styleUrl: './import-dialog.scss',
})
export class ImportDialog {
  private readonly http = inject(HttpClient);
  private readonly dialogRef = inject<MatDialogRef<ImportDialog, ImportResult>>(MatDialogRef);
  protected readonly data = inject<ImportDialogData>(MAT_DIALOG_DATA);

  protected readonly file = signal<File | null>(null);
  protected readonly busy = signal(false);
  protected readonly preview = signal<ImportResult | null>(null);
  protected readonly problems = signal<Problem[]>([]);
  protected readonly error = signal<string | null>(null);
  private request?: Subscription;

  protected readonly hasChanges = computed(() => {
    const preview = this.preview();
    return preview !== null && preview.created + preview.updated > 0;
  });

  protected choose(files: FileList | null): void {
    const file = files?.item(0) ?? null;
    this.file.set(file);
    if (file) {
      this.send(file, true);
    }
  }

  protected import(): void {
    const file = this.file();
    if (file) {
      this.send(file, false);
    }
  }

  private send(file: File, dryRun: boolean): void {
    const body = new FormData();
    body.append('file', file);
    this.busy.set(true);
    this.preview.set(null);
    this.problems.set([]);
    this.error.set(null);
    // A check still running for an earlier file must not land on top of this one.
    this.request?.unsubscribe();
    this.request = this.http
      .post<ImportResult>(this.data.url, body, { params: { dryRun } })
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          if (dryRun) {
            this.preview.set(result);
          } else {
            this.dialogRef.close(result);
          }
        },
        error: (error: unknown) => {
          this.busy.set(false);
          const problems = importProblems(error);
          if (problems.length > 0) {
            this.problems.set(problems);
          } else {
            this.error.set(errorMessage(error));
          }
        },
      });
  }
}

/** The 400 of a bad file lists its problems under keys "File" and "Row 7"; translated here. */
function importProblems(error: unknown): Problem[] {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return [];
  }
  const errors = (error.error as ProblemDetails | null)?.errors ?? {};
  return Object.entries(errors).map(([key, messages]) => ({
    where: key === 'File' ? t('import.file') : t('import.row', { row: key.replace('Row ', '') }),
    messages,
  }));
}

/** Opens the import dialog; emits the result only when something was imported. */
export function openImport(dialog: MatDialog, data: ImportDialogData): Observable<ImportResult> {
  return dialog
    .open<ImportDialog, ImportDialogData, ImportResult>(ImportDialog, { data, width: '560px' })
    .afterClosed()
    .pipe(filter((result): result is ImportResult => result !== undefined));
}
