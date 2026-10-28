import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService, UserRole, roleLabel } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/problem';
import { TranslatePipe } from '../../core/i18n/i18n';
import { LanguagePicker } from '../../shared/language-picker/language-picker';

/** The seeded demo accounts (DemoDataSeeder); the password is public on purpose. */
const DEMO_ACCOUNTS: { role: UserRole; icon: string; email: string }[] = [
  { role: 'Admin', icon: 'shield_person', email: 'admin@workshop.example.com' },
  { role: 'Planner', icon: 'event_note', email: 'planner@workshop.example.com' },
  { role: 'Warehouse', icon: 'inventory_2', email: 'warehouse@workshop.example.com' },
];
const DEMO_PASSWORD = 'workshop-demo';

@Component({
  selector: 'app-login',
  imports: [
    TranslatePipe,
    LanguagePicker,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <main class="login">
      <section class="card">
        <span class="mark" aria-hidden="true"><mat-icon>carpenter</mat-icon></span>
        <h1>{{ 'login.title' | t }}</h1>
        <p class="lead">{{ 'login.lead' | t }}</p>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'login.email' | t }}</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="username" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'login.password' | t }}</mat-label>
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
          <button mat-flat-button type="submit" class="submit" [disabled]="busy()">
            {{ 'login.submit' | t }}
          </button>
        </form>

        <div class="demo">
          <p class="muted">{{ 'login.tryDemo' | t }}</p>
          <div class="roles">
            @for (account of demoAccounts; track account.email) {
              <button
                type="button"
                class="role"
                (click)="signInAs(account.email)"
                [disabled]="busy()"
                [attr.aria-label]="'login.signInAs' | t: { role: roleLabel(account.role) }"
              >
                <mat-icon>{{ account.icon }}</mat-icon>
                {{ roleLabel(account.role) }}
              </button>
            }
          </div>
          <p class="hint">
            {{ 'login.demoPassword' | t }} <span class="code">${DEMO_PASSWORD}</span>
          </p>
        </div>
        <app-language-picker class="language" />
      </section>
    </main>
  `,
  styles: `
    .login {
      min-height: 100dvh;
      display: grid;
      place-items: center;
      padding: 24px 16px;
      box-sizing: border-box;
    }
    .card {
      width: min(400px, 100%);
      box-sizing: border-box;
      padding: 36px 32px 28px;
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      background: var(--surface);
      text-align: center;
    }
    /* The brand mark from the top bar, larger: a plain square, no gradient. */
    .mark {
      display: inline-grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 9px;
      background: var(--text);
      color: var(--bg);
    }
    .mark mat-icon {
      font-size: 22px;
      width: 22px;
      height: 22px;
    }
    h1 {
      margin: 18px 0 6px;
      font-size: 24px;
      font-weight: 650;
      letter-spacing: -0.02em;
    }
    .lead {
      margin: 0 0 28px;
      color: var(--text-2);
    }
    form {
      display: grid;
      gap: 2px;
      text-align: left;
    }
    .submit {
      height: 42px;
      font-size: 15px;
    }
    .error-text {
      margin: 0 0 10px;
    }
    .demo {
      margin-top: 28px;
      padding-top: 20px;
      border-top: 1px solid var(--border-soft);
      font-size: 13.5px;
    }
    .demo p {
      margin: 0;
    }
    .roles {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 8px;
      margin: 12px 0;
    }
    .role {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      height: 34px;
      padding: 0 12px;
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      background: var(--surface);
      color: var(--text);
      font: inherit;
      font-weight: 550;
      cursor: pointer;
      transition: background-color 0.15s var(--ease);
    }
    .role:hover:enabled {
      background: var(--surface-muted);
    }
    .role:disabled {
      opacity: 0.6;
      cursor: default;
    }
    .role mat-icon {
      font-size: 17px;
      width: 17px;
      height: 17px;
      color: var(--text-2);
    }
    .hint {
      color: var(--text-3);
    }
    .language {
      max-width: 200px;
      margin: 20px auto 0;
    }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Query parameter set by the guard or the interceptor; bound by withComponentInputBinding(). */
  readonly returnUrl = input<string>();

  protected readonly demoAccounts = DEMO_ACCOUNTS;
  protected readonly roleLabel = roleLabel;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  /** One click: fills in the demo account so the visitor sees what was used, then signs in. */
  protected signInAs(email: string): void {
    this.form.setValue({ email, password: DEMO_PASSWORD });
    this.submit();
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
