import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/problem';

/** The seeded demo accounts (DemoDataSeeder); the password is public on purpose. */
const DEMO_ACCOUNTS = [
  { role: 'Admin', email: 'admin@workshop.example.com' },
  { role: 'Planner', email: 'planner@workshop.example.com' },
  { role: 'Warehouse', email: 'warehouse@workshop.example.com' },
];
const DEMO_PASSWORD = 'workshop-demo';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <main class="login">
      <section class="panel card">
        <header class="panel-header">
          <h1 class="panel-title">Sign in to Workshop panel</h1>
        </header>
        <form class="panel-body" [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="username" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Password</mat-label>
            <input
              matInput
              type="password"
              formControlName="password"
              autocomplete="current-password"
            />
          </mat-form-field>
          @if (error()) {
            <p class="error-text" role="alert">{{ error() }}</p>
          }
          <button mat-flat-button type="submit" [disabled]="busy()">Sign in</button>
        </form>
        <footer class="demo">
          <p class="muted">Demo accounts, password <span class="code">${DEMO_PASSWORD}</span>:</p>
          <ul>
            @for (account of demoAccounts; track account.email) {
              <li>
                <button type="button" class="link" (click)="fill(account.email)">
                  {{ account.role }}
                </button>
                <span class="code">{{ account.email }}</span>
              </li>
            }
          </ul>
        </footer>
      </section>
    </main>
  `,
  styles: `
    .login {
      min-height: 100dvh;
      display: grid;
      place-items: center;
      padding: 16px;
      background: var(--app-bg);
    }
    .card {
      width: min(400px, 100%);
    }
    form {
      display: grid;
      gap: 4px;
    }
    .demo {
      padding: 0 16px 16px;
      font-size: 13px;
    }
    .demo ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 4px;
    }
    .link {
      min-width: 80px;
      padding: 0;
      border: 0;
      background: none;
      color: var(--app-text);
      font: inherit;
      font-weight: 600;
      text-align: left;
      text-decoration: underline;
      cursor: pointer;
    }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Query parameter set by the guard or the interceptor; bound by withComponentInputBinding(). */
  readonly returnUrl = input<string>();

  protected readonly demoAccounts = DEMO_ACCOUNTS;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected fill(email: string): void {
    this.form.setValue({ email, password: DEMO_PASSWORD });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { email, password } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(email, password).subscribe({
      next: () => {
        // navigateByUrl only routes inside the app, so returnUrl cannot send the user to another site.
        const returnUrl = this.returnUrl() ?? '';
        void this.router.navigateByUrl(returnUrl.startsWith('/') ? returnUrl : '/dashboard');
      },
      error: (error: unknown) => {
        this.error.set(errorMessage(error));
        this.busy.set(false);
      },
    });
  }
}
