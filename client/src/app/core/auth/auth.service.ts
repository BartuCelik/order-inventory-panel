import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api';
import { t } from '../i18n/i18n';

export type UserRole = 'Admin' | 'Planner' | 'Warehouse';

export interface SessionUser {
  email: string;
  displayName: string;
  role: UserRole;
}

/** What POST /auth/login returns. */
export interface Session {
  token: string;
  expiresAt: string;
  user: SessionUser;
}

const STORAGE_KEY = 'workshop.session';

/**
 * The signed-in user and their token. The token lives in localStorage so a reload keeps the session;
 * the API enforces every permission, so the role here only decides which buttons to show.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<Session | null>(readStoredSession());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);

  /** Mirrors the API's role lists (Api/Auth/AuthenticationSetup.cs, CLAUDE.md roles table). */
  readonly canPlan = computed(() => hasRole(this.user(), 'Admin', 'Planner'));
  readonly canHandleStock = computed(() => hasRole(this.user(), 'Admin', 'Warehouse'));

  isSignedIn(): boolean {
    const session = this.session();
    if (session && new Date(session.expiresAt).getTime() <= Date.now()) {
      this.clear();
      return false;
    }
    return session !== null;
  }

  login(email: string, password: string): Observable<Session> {
    return this.http
      .post<Session>(`${API_BASE_URL}/auth/login`, { email, password })
      .pipe(tap((session) => this.store(session)));
  }

  /** Signs out and goes to the login page; `returnUrl` brings the user back after signing in again. */
  logout(returnUrl?: string): void {
    this.clear();
    void this.router.navigate(['/login'], {
      queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : {},
    });
  }

  private store(session: Session): void {
    this.session.set(session);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Storage blocked (private mode): the session still works until the tab is closed.
    }
  }

  private clear(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing stored to clear.
    }
  }
}

export function roleLabel(role: UserRole): string {
  return t(`role.${role}`);
}

function hasRole(user: SessionUser | null, ...roles: UserRole[]): boolean {
  return user !== null && roles.includes(user.role);
}

function readStoredSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}
