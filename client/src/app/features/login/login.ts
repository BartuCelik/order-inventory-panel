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
        <span class="app-icon" aria-hidden="true"><mat-icon>carpenter</mat-icon></span>
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
                (click)="fill(account.email)"
                [attr.aria-label]="'login.fillDemo' | t: { role: roleLabel(account.role) }"
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
    }
    .card {
      width: min(420px, 100%);
      box-sizing: border-box;
      padding: 40px 32px 28px;
      border: 1px solid var(--border);
      border-radius: 28px;
      background: var(--surface);
      -webkit-backdrop-filter: var(--glass);
      backdrop-filter: var(--glass);
      box-shadow:
        inset 0 1px 0 var(--highlight),
        var(--shadow-lift);
      text-align: center;
      animation: rise 0.5s var(--ease) both;
    }
    @keyframes rise {
      from {
        opacity: 0;
        transform: translateY(12px) scale(0.98);
      }
    }
    .app-icon {
      display: inline-grid;
      place-items: center;
      width: 64px;
      height: 64px;
      border-radius: 16px;
      background: linear-gradient(145deg, #0a84ff, #5e5ce6 55%, #bf5af2);
      box-shadow:
        inset 0 1px 0 rgb(255 255 255 / 35%),
        0 10px 24px -6px rgb(10 132 255 / 50%);
      color: #fff;
    }
    .app-icon mat-icon {
      font-size: 34px;
      width: 34px;
      height: 34px;
    }
    h1 {
      margin: 20px 0 6px;
      font-size: 28px;
      font-weight: 700;
      letter-spacing: -0.028em;
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
      height: 44px;
      font-size: 16px;
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
      margin: 10px 0;
    }
    .role {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      height: 34px;
      padding: 0 14px;
      border: 0;
      border-radius: 999px;
      background: var(--accent-soft);
      color: var(--accent);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
      transition: transform 0.15s var(--ease);
    }
    .role:hover {
      transform: translateY(-1px);
    }
    .role mat-icon {
      font-size: 17px;
      width: 17px;
      height: 17px;
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
